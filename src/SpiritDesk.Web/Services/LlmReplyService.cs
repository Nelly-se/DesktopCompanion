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

    public async Task<string> GenerateReplyAsync(
        SpiritDefinition spirit,
        string nickname,
        string userMessage,
        string fallbackReply)
    {
        var scriptedReply = BuildScriptedReply(spirit, nickname, userMessage);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("LLM fallback: ApiKey is empty.");
            return scriptedReply ?? fallbackReply;
        }

        var endpoint = BuildCompletionsEndpoint(_baseUrl);

        try
        {
            var payloadJson = JsonSerializer.Serialize(new
            {
                model = _model,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content =
                            $"你是 SpiritDesk 的桌面陪伴精灵。用户昵称是 {SafeName(nickname)}；精灵名称是 {spirit.Name}；MBTI={spirit.Mbti}；称号={spirit.Title}；核心定位={spirit.CoreRole}；性格描述={spirit.Personality}；详细人设={spirit.Description}；特殊机制={spirit.SpecialMechanism}。请使用简洁自然的中文回复，保持温暖、鼓励和符合当前精灵人设。"
                    },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.8
            });

            using var response = await SendWithRetryAsync(endpoint, payloadJson);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "LLM fallback: request failed. status={StatusCode}, endpoint={Endpoint}, model={Model}, body={Body}",
                    (int)response.StatusCode,
                    endpoint,
                    _model,
                    Truncate(body));
                return scriptedReply ?? fallbackReply;
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("LLM fallback: empty content. endpoint={Endpoint}, model={Model}", endpoint, _model);
                return scriptedReply ?? fallbackReply;
            }

            logger.LogInformation("LLM success. endpoint={Endpoint}, model={Model}", endpoint, _model);
            return content.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM fallback: exception. endpoint={Endpoint}, model={Model}", endpoint, _model);
            return scriptedReply ?? fallbackReply;
        }
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
            return $"{spirit.Name} 给你一版答辩演示脚本：先介绍项目定位，再演示“建档选精灵、主页工作台、聊天、任务、互动、小游戏”，最后补充技术选型与五精灵差异化机制。开场时你可以这样说：大家好，我们做的是一个基于 C# 的桌面伴侣 Agent 系统。";
        }

        if (ContainsAny(message, "今天最重要", "三件事", "最重要的三件事"))
        {
            return $"{name}，我建议你先做三件事：1. 跑完整个首页演示流程。2. 检查五精灵加成是否都有界面反馈。3. 用一轮真实聊天把 Agent 能力展示出来。";
        }

        if (ContainsAny(message, "总结", "复盘", "梳理"))
        {
            return "可以，我帮你快速总结：1. 已完成桌面壳与本地数据档案。2. 已完成五精灵选择与差异化陪伴。3. 已完成聊天、任务、互动和小游戏闭环。4. 接下来重点检查演示流程与答辩话术。";
        }

        if (ContainsAny(message, "任务拆解", "拆解任务", "怎么做"))
        {
            return "这类任务我们可以这样拆：先确认目标，再列出 3 个最小步骤，然后先做最容易启动的一步。如果你愿意，我还可以继续按当前项目内容帮你细拆。";
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

    private static string SafeName(string nickname)
    {
        return string.IsNullOrWhiteSpace(nickname) ? "你" : nickname.Trim();
    }

    private static string Truncate(string text, int max = 400)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..max];
    }

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
                return await httpClient.SendAsync(request);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await Task.Delay(300);
            }
        }

        throw new HttpRequestException("LLM request failed after retry.");
    }

    private static bool ContainsAny(string input, params string[] keywords)
    {
        return keywords.Any(keyword => input.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
