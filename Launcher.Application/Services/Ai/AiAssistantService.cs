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

namespace Launcher.Application.Services.Ai;

/// <summary>
/// AI 助手的编排入口：配置齐全时走在线模型，未配置或调用失败时退回本地知识库，
/// 保证任何网络状态下助手都有回答。
/// </summary>
public sealed class AiAssistantService
{
    private readonly IAiChatService chatService;

    public AiAssistantService(IAiChatService chatService)
    {
        this.chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
    }

    public string Greeting => OfflineAssistantKnowledge.Greeting;

    public IReadOnlyList<string> Suggestions => OfflineAssistantKnowledge.Suggestions;

    /// <summary>
    /// 提问。<paramref name="history"/> 只包含之前的对话，本次问题由 <paramref name="question"/> 单独给出，
    /// 便于在线失败时用原始问题命中本地知识库。
    /// </summary>
    public async Task<AiChatReply> AskAsync(
        AiChatOptions options,
        IReadOnlyList<AiChatMessage> history,
        string question,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(history);

        if (string.IsNullOrWhiteSpace(question))
            return AiChatReply.Failure(AiFailureCodes.EmptyQuestion);

        if (!options.IsConfigured)
            return AiChatReply.Offline(OfflineAssistantKnowledge.Answer(question));

        var messages = new List<AiChatMessage>(history.Count + 1);
        messages.AddRange(history);
        messages.Add(new AiChatMessage(AiChatRole.User, question));

        AiChatReply reply;
        try
        {
            reply = await chatService.CompleteAsync(options, messages, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // 网络异常统一折算成降级，具体的错误细节由 Infrastructure 记录进日志。
            reply = AiChatReply.Failure(AiFailureCodes.Network);
        }

        if (reply.IsSuccess && !string.IsNullOrWhiteSpace(reply.Content))
            return reply;

        // 在线失败：用本地知识库兜底，同时把失败码带出去，让界面提示“这次是离线回答”。
        var offline = AiChatReply.Offline(OfflineAssistantKnowledge.Answer(question));
        return offline with { FailureCode = reply.FailureCode ?? AiFailureCodes.Unknown };
    }

    public Task<AiChatReply> ProbeAsync(AiChatOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsConfigured)
            return Task.FromResult(AiChatReply.Failure(AiFailureCodes.NotConfigured));

        return chatService.ProbeAsync(options, cancellationToken);
    }
}

/// <summary>
/// 失败码。界面据此选择资源文件里的提示语，不直接展示底层异常。
/// </summary>
public static class AiFailureCodes
{
    public const string EmptyQuestion = "empty_question";
    public const string NotConfigured = "not_configured";
    public const string Network = "network";
    public const string Unauthorized = "unauthorized";
    public const string RateLimited = "rate_limited";
    public const string ModelNotFound = "model_not_found";
    public const string InvalidEndpoint = "invalid_endpoint";
    public const string EmptyResponse = "empty_response";
    public const string TooLong = "too_long";
    public const string Unknown = "unknown";
}
