/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 *
 * SPDX-License-Identifier: GPL-3.0-only
 */

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Application.Services.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.Infrastructure.Ai;

/// <summary>
/// 兼容 OpenAI Chat Completions 协议的客户端。绝大多数国内模型服务（DeepSeek、通义、Kimi、
/// 智谱、硅基流动、Ollama 等）都支持这套接口，填不同的 Base URL 即可切换。
/// 这里只做协议转换和错误归类，不外泄底层异常文本。
/// </summary>
public sealed class OpenAiCompatibleAiChatService : IAiChatService, IDisposable
{
    private const int MaxHistoryMessages = 24;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(90);

    private readonly HttpClient httpClient;
    private readonly bool ownsClient;
    private readonly ILogger<OpenAiCompatibleAiChatService> logger;

    public OpenAiCompatibleAiChatService(
        HttpClient? httpClient = null,
        ILogger<OpenAiCompatibleAiChatService>? logger = null)
    {
        ownsClient = httpClient is null;
        this.httpClient = httpClient ?? new HttpClient();
        this.logger = logger ?? NullLogger<OpenAiCompatibleAiChatService>.Instance;
    }

    public async Task<AiChatReply> CompleteAsync(
        AiChatOptions options,
        IReadOnlyList<AiChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(messages);

        if (!options.IsConfigured)
            return AiChatReply.Failure(AiFailureCodes.NotConfigured);

        if (!TryBuildRequestUri(options.Endpoint, out var uri))
        {
            logger.LogWarning("AI chat request rejected: endpoint is not a valid absolute http(s) url.");
            return AiChatReply.Failure(AiFailureCodes.InvalidEndpoint);
        }

        var payload = BuildPayload(options, messages, maxTokens: null);
        if (payload is null)
            return AiChatReply.Failure(AiFailureCodes.TooLong);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var failure = MapStatusCode(response.StatusCode);
                logger.LogWarning(
                    "AI chat request failed. Status={Status} Code={Code} Endpoint={Endpoint}",
                    (int)response.StatusCode,
                    failure,
                    uri.GetLeftPart(UriPartial.Path));
                return AiChatReply.Failure(failure);
            }

            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var content = ExtractContent(body);
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("AI chat response contained no assistant content. Length={Length}", body.Length);
                return AiChatReply.Failure(AiFailureCodes.EmptyResponse);
            }

            logger.LogDebug("AI chat request succeeded. Length={Length}", content.Length);
            return AiChatReply.Online(content.Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("AI chat request timed out after {Seconds}s.", RequestTimeout.TotalSeconds);
            return AiChatReply.Failure(AiFailureCodes.Network);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "AI chat request failed with a transport error.");
            return AiChatReply.Failure(AiFailureCodes.Network);
        }
    }

    public async Task<AiChatReply> ProbeAsync(AiChatOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var probe = await CompleteAsync(
            options,
            [new AiChatMessage(AiChatRole.User, "ping")],
            cancellationToken).ConfigureAwait(false);

        return probe.IsSuccess ? AiChatReply.Online(string.Empty) : probe;
    }

    /// <summary>
    /// 接受几种常见写法：完整 .../chat/completions、以 /v1 结尾的 Base URL、以及只有域名的根地址。
    /// </summary>
    internal static bool TryBuildRequestUri(string? endpoint, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        var trimmed = endpoint.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed))
            return false;

        if (parsed.Scheme is not ("http" or "https"))
            return false;

        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            uri = parsed;
            return true;
        }

        var builder = new UriBuilder(parsed);
        var path = builder.Path.TrimEnd('/');
        if (path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            builder.Path = path + "/chat/completions";
        else if (path.Length == 0)
            builder.Path = "/v1/chat/completions";
        else
            builder.Path = path + "/v1/chat/completions";

        uri = builder.Uri;
        return true;
    }

    private static string? BuildPayload(AiChatOptions options, IReadOnlyList<AiChatMessage> messages, int? maxTokens)
    {
        var payload = new ChatRequest
        {
            Model = options.Model.Trim(),
            Stream = false,
            Temperature = 0.6,
            MaxTokens = maxTokens
        };

        if (!string.IsNullOrWhiteSpace(options.SystemPrompt))
            payload.Messages.Add(new ChatRequestMessage { Role = "system", Content = options.SystemPrompt });

        // 只回传最近若干条，避免上下文无限增长导致请求体过大。
        var start = Math.Max(0, messages.Count - MaxHistoryMessages);
        for (var i = start; i < messages.Count; i++)
        {
            var message = messages[i];
            if (string.IsNullOrWhiteSpace(message.Content))
                continue;

            payload.Messages.Add(new ChatRequestMessage
            {
                Role = message.Role == AiChatRole.User ? "user" : "assistant",
                Content = message.Content
            });
        }

        if (payload.Messages.Count == 0)
            return null;

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string? ExtractContent(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString();
                }

                if (first.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    return text.GetString();
            }

            // 部分兼容实现会在顶层直接返回 content 或 output_text。
            if (root.TryGetProperty("content", out var direct) && direct.ValueKind == JsonValueKind.String)
                return direct.GetString();

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string MapStatusCode(HttpStatusCode status)
    {
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiFailureCodes.Unauthorized,
            HttpStatusCode.NotFound => AiFailureCodes.ModelNotFound,
            HttpStatusCode.TooManyRequests => AiFailureCodes.RateLimited,
            HttpStatusCode.RequestEntityTooLarge => AiFailureCodes.TooLong,
            _ => AiFailureCodes.Network
        };
    }

    public void Dispose()
    {
        if (ownsClient)
            httpClient.Dispose();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatRequestMessage> Messages { get; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
    }

    private sealed class ChatRequestMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
