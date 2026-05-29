// =============================================================================
// LlmReplyService.cs — 大模型 HTTP 客户端（OpenAI 兼容 / 火山 Ark）
// =============================================================================
// 数据结构：
//   - IReadOnlyList&lt;ChatMessage&gt;?：最近对话上下文，序列化为 JSON messages 数组
//   - 匿名对象 new { model, messages }：JsonSerializer.Serialize 的临时 DTO
// C# 语法：
//   - readonly 字段：构造后不变（HttpClient、配置）
//   - ?? 合并配置项；FirstNonEmpty 选第一个非空字符串
//   - using var response：IDisposable 自动释放 HttpResponseMessage
// =============================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Services;

/// <summary>Chat Completions；无 Key 或失败时上层用 SpiritPersonaService 规则回复。</summary>
public class LlmReplyService
{
    private const string DefaultOpenAiBaseUrl = "https://api.openai.com/v1";
    private const string DefaultOpenAiModel = "gpt-4.1-mini";

    private readonly HttpClient _httpClient;
    private readonly ILogger<LlmReplyService> _logger;
    private readonly SpiritPersonaService _personaService;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly string _providerName;

    public LlmReplyService(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<LlmReplyService> logger,
        SpiritPersonaService personaService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _personaService = personaService;
        _apiKey = FirstNonEmpty(
            configuration["ARK_API_KEY"],
            configuration["Llm:ApiKey"],
            configuration["OpenAI:ApiKey"]);
        _model = FirstNonEmpty(
                     configuration["ARK_MODEL"],
                     configuration["Llm:Model"],
                     configuration["OpenAI:Model"])
                 ?? DefaultOpenAiModel;
        _baseUrl = FirstNonEmpty(
                       configuration["ARK_API_BASE"],
                       configuration["Llm:BaseUrl"],
                       configuration["OpenAI:BaseUrl"])
                   ?? DefaultOpenAiBaseUrl;
        _providerName = ResolveProviderName(configuration);
    }

    public async Task<string> GenerateReplyAsync(
        SpiritDefinition spirit,
        string nickname,
        string userMessage,
        string fallbackReply,
        IReadOnlyList<ChatMessage>? recentConversation = null)
    {
        var scriptedReply = BuildScriptedReply(spirit, nickname, userMessage);
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("No LLM API key is configured for {Provider}. Falling back to scripted reply.", _providerName);
            return scriptedReply ?? fallbackReply;
        }

        var endpoint = BuildCompletionsEndpoint(_baseUrl);
        try
        {
            var payloadJson = JsonSerializer.Serialize(new
            {
                model = _model,
                messages = BuildMessages(spirit, nickname, userMessage, recentConversation),
                temperature = 0.85
            });

            using var response = await SendWithRetryAsync(endpoint, payloadJson);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "{Provider} request failed with status {StatusCode}. Response preview: {BodyPreview}",
                    _providerName,
                    (int)response.StatusCode,
                    SummarizeForLog(body));
                return scriptedReply ?? fallbackReply;
            }

            using var doc = JsonDocument.Parse(body);
            var content = ExtractAssistantContent(doc.RootElement);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("{Provider} returned an empty assistant message. Falling back to scripted reply.", _providerName);
                return scriptedReply ?? fallbackReply;
            }

            return content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Provider} request failed. Falling back to scripted reply.", _providerName);
            return scriptedReply ?? fallbackReply;
        }
    }

    public async Task<string> GenerateReplyStreamAsync(
        SpiritDefinition spirit,
        string nickname,
        string userMessage,
        string fallbackReply,
        IReadOnlyList<ChatMessage>? recentConversation,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        var scriptedReply = BuildScriptedReply(spirit, nickname, userMessage);
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("No LLM API key is configured for {Provider}. Falling back to scripted reply.", _providerName);
            return await EmitFallbackReplyAsync(scriptedReply ?? fallbackReply, onChunk, cancellationToken);
        }

        var endpoint = BuildCompletionsEndpoint(_baseUrl);
        try
        {
            var payloadJson = JsonSerializer.Serialize(new
            {
                model = _model,
                messages = BuildMessages(spirit, nickname, userMessage, recentConversation),
                temperature = 0.85,
                stream = true
            });

            using var response = await SendStreamingWithRetryAsync(endpoint, payloadJson, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "{Provider} streaming request failed with status {StatusCode}. Response preview: {BodyPreview}",
                    _providerName,
                    (int)response.StatusCode,
                    SummarizeForLog(body));
                return await EmitFallbackReplyAsync(scriptedReply ?? fallbackReply, onChunk, cancellationToken);
            }

            var streamedReply = await ReadStreamingReplyAsync(response, onChunk, cancellationToken);
            if (string.IsNullOrWhiteSpace(streamedReply))
            {
                _logger.LogWarning("{Provider} returned an empty streaming assistant message. Falling back to scripted reply.", _providerName);
                return await EmitFallbackReplyAsync(scriptedReply ?? fallbackReply, onChunk, cancellationToken);
            }

            return streamedReply.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "{Provider} streaming request failed. Falling back to scripted reply.", _providerName);
            return await EmitFallbackReplyAsync(scriptedReply ?? fallbackReply, onChunk, cancellationToken);
        }
    }

    private object[] BuildMessages(
        SpiritDefinition spirit,
        string nickname,
        string userMessage,
        IReadOnlyList<ChatMessage>? recentConversation)
    {
        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = _personaService.BuildLlmSystemPrompt(spirit, nickname)
            }
        };

        if (recentConversation is not null)
        {
            foreach (var message in recentConversation.TakeLast(8))
            {
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                messages.Add(new
                {
                    role = message.Sender == "user" ? "user" : "assistant",
                    content = message.Content.Trim()
                });
            }
        }

        messages.Add(new
        {
            role = "user",
            content = userMessage.Trim()
        });

        return messages.ToArray();
    }

    private static string? BuildScriptedReply(SpiritDefinition spirit, string nickname, string userMessage)
    {
        var name = SafeName(nickname);
        var message = userMessage.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (ContainsAny(message, "答辩", "演示", "汇报"))
        {
            return $"{spirit.Name} 建议答辩顺序：项目定位 → 核心闭环 → 关键功能演示 → 技术选型与取舍。";
        }

        if (ContainsAny(message, "今天最重要", "三件事"))
        {
            return $"{name}，建议你先做三件事：1）跑通完整演示流程；2）确认五精灵加成有页面反馈；3）准备 1 分钟讲解词。";
        }

        if (ContainsAny(message, "总结", "复盘", "梳理"))
        {
            return "可以快速复盘为四段：目标、动作、结果、下一步。";
        }

        if (ContainsAny(message, "任务拆解", "怎么做"))
        {
            return "先确认目标，再拆成 3 个最小动作，先做最容易启动的一步。";
        }

        return null;
    }

    private static string BuildCompletionsEndpoint(string baseUrl)
    {
        var normalized = baseUrl.Trim().TrimEnd('/');
        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return $"{normalized}/chat/completions";
    }

    private static string SafeName(string nickname) => string.IsNullOrWhiteSpace(nickname) ? "你" : nickname.Trim();

    private async Task<HttpResponseMessage> SendWithRetryAsync(string endpoint, string payloadJson)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Version = HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            request.Headers.ConnectionClose = true;
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            try
            {
                return await _httpClient.SendAsync(request);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await Task.Delay(300);
            }
        }

        throw new HttpRequestException("LLM request failed after retry.");
    }

    private async Task<HttpResponseMessage> SendStreamingWithRetryAsync(string endpoint, string payloadJson, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Version = HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            request.Headers.ConnectionClose = true;
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            try
            {
                return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await Task.Delay(300, cancellationToken);
            }
        }

        throw new HttpRequestException("LLM streaming request failed after retry.");
    }

    private static async Task<string> ReadStreamingReplyAsync(
        HttpResponseMessage response,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken)
    {
        var reply = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]")
            {
                break;
            }

            var chunk = ExtractStreamingContent(data);
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            reply.Append(chunk);
            await onChunk(chunk);
        }

        return reply.ToString();
    }

    private static string? ExtractStreamingContent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var firstChoice = choices[0];
        if (firstChoice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var deltaContent))
        {
            return ReadContentElement(deltaContent);
        }

        if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var messageContent))
        {
            return ReadContentElement(messageContent);
        }

        return null;
    }

    private static async Task<string> EmitFallbackReplyAsync(string reply, Func<string, Task> onChunk, CancellationToken cancellationToken)
    {
        var trimmed = reply.Trim();
        foreach (var chunk in SplitForStreaming(trimmed))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await onChunk(chunk);
            await Task.Delay(18, cancellationToken);
        }

        return trimmed;
    }

    private static string? ExtractAssistantContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        return ReadContentElement(content);
    }

    private static string? ReadContentElement(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Array => string.Concat(content.EnumerateArray()
                .Select(ReadContentPart)
                .Where(static text => !string.IsNullOrWhiteSpace(text))),
            _ => null
        };
    }

    private static string? ReadContentPart(JsonElement item)
    {
        return item.ValueKind switch
        {
            JsonValueKind.String => item.GetString(),
            JsonValueKind.Object when item.TryGetProperty("text", out var textElement) => textElement.GetString(),
            JsonValueKind.Object when item.TryGetProperty("content", out var contentElement) => contentElement.GetString(),
            _ => null
        };
    }

    private static string ResolveProviderName(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration["ARK_API_KEY"])
               || !string.IsNullOrWhiteSpace(configuration["ARK_API_BASE"])
               || !string.IsNullOrWhiteSpace(configuration["ARK_MODEL"])
            ? "Volcengine Ark"
            : "OpenAI-compatible LLM";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string SummarizeForLog(string body)
    {
        const int maxLength = 280;
        var singleLine = body.ReplaceLineEndings(" ").Trim();
        if (singleLine.Length <= maxLength)
        {
            return singleLine;
        }

        return singleLine[..maxLength] + "...";
    }

    private static IEnumerable<string> SplitForStreaming(string text)
    {
        const int maxChunkLength = 8;
        var chunk = new StringBuilder();
        foreach (var ch in text)
        {
            chunk.Append(ch);
            if (chunk.Length >= maxChunkLength || "，。！？；,.!?;".Contains(ch))
            {
                yield return chunk.ToString();
                chunk.Clear();
            }
        }

        if (chunk.Length > 0)
        {
            yield return chunk.ToString();
        }
    }

    private static bool ContainsAny(string input, params string[] keywords) => keywords.Any(keyword => input.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
