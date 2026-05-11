using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Services;

public class LlmReplyService(IConfiguration configuration, HttpClient httpClient, ILogger<LlmReplyService> logger)
{
    private readonly string? _apiKey = configuration["OpenAI:ApiKey"];
    private readonly string _model = configuration["OpenAI:Model"] ?? "gpt-4.1-mini";
    private readonly string _baseUrl = configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";

    public async Task<string> GenerateReplyAsync(SpiritDefinition spirit, string nickname, string userMessage, string fallbackReply)
    {
        var scriptedReply = BuildScriptedReply(spirit, nickname, userMessage);
        if (string.IsNullOrWhiteSpace(_apiKey)) return scriptedReply ?? fallbackReply;

        var endpoint = BuildCompletionsEndpoint(_baseUrl);
        try
        {
            var payloadJson = JsonSerializer.Serialize(new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = $"你是 SpiritDesk 的桌面精灵。用户昵称：{SafeName(nickname)}；精灵：{spirit.Name}；称号：{spirit.Title}；核心定位：{spirit.CoreRole}；性格：{spirit.Personality}。请用自然、简洁、温暖的中文回复，并保持当前精灵人设。" },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.8
            });

            using var response = await SendWithRetryAsync(endpoint, payloadJson);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return scriptedReply ?? fallbackReply;

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(content) ? scriptedReply ?? fallbackReply : content.Trim();
        }
        catch
        {
            return scriptedReply ?? fallbackReply;
        }
    }

    private static string? BuildScriptedReply(SpiritDefinition spirit, string nickname, string userMessage)
    {
        var name = SafeName(nickname);
        var message = userMessage.Trim();
        if (string.IsNullOrWhiteSpace(message)) return null;

        if (ContainsAny(message, "答辩", "演示", "汇报"))
            return $"{spirit.Name} 建议答辩顺序：项目定位 → 核心闭环 → 关键功能演示 → 技术选型与取舍。";

        if (ContainsAny(message, "今天最重要", "三件事"))
            return $"{name}，建议你先做三件事：1）跑通完整演示流程；2）确认五精灵加成有页面反馈；3）准备 1 分钟讲解词。";

        if (ContainsAny(message, "总结", "复盘", "梳理"))
            return "可以快速复盘为四段：目标、动作、结果、下一步。";

        if (ContainsAny(message, "任务拆解", "怎么做"))
            return "先确认目标，再拆成 3 个最小动作，先做最容易启动的一步。";

        return null;
    }

    private static string BuildCompletionsEndpoint(string baseUrl)
    {
        var normalized = baseUrl.Trim().TrimEnd('/');
        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return normalized;
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
            try { return await httpClient.SendAsync(request); }
            catch (HttpRequestException) when (attempt < maxAttempts) { await Task.Delay(300); }
        }
        throw new HttpRequestException("LLM request failed after retry.");
    }

    private static bool ContainsAny(string input, params string[] keywords) => keywords.Any(keyword => input.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
