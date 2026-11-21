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
/// 对话中的角色。离线知识库只会产生 <see cref="Assistant"/>，在线模型两者都会出现。
/// </summary>
public enum AiChatRole
{
    User,
    Assistant
}

/// <summary>
/// 一条对话消息。领域层不关心模型厂商的字段名，转换在 Infrastructure 边界完成。
/// </summary>
public sealed record AiChatMessage(AiChatRole Role, string Content);

/// <summary>
/// AI 助手的连接配置。缺省值代表“未配置”，此时走离线知识库。
/// </summary>
public sealed record AiChatOptions(
    string Endpoint,
    string ApiKey,
    string Model,
    string SystemPrompt)
{
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    public bool IsConfigured =>
        HasApiKey
        && !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Model);
}

/// <summary>
/// 一次问答的结果。<see cref="IsOffline"/> 为 true 表示由本地知识库作答，
/// UI 需要据此提示用户当前回答不来自在线模型。
/// </summary>
public sealed record AiChatReply(
    bool IsSuccess,
    string Content,
    bool IsOffline,
    string? FailureCode = null)
{
    public static AiChatReply Offline(string content) => new(true, content, true);

    public static AiChatReply Online(string content) => new(true, content, false);

    public static AiChatReply Failure(string code) => new(false, string.Empty, false, code);
}

/// <summary>
/// 在线对话模型接口。实现方负责鉴权、请求组装和协议错误映射。
/// </summary>
public interface IAiChatService
{
    Task<AiChatReply> CompleteAsync(
        AiChatOptions options,
        IReadOnlyList<AiChatMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 校验配置是否可用，供设置面板的“测试连接”使用。
    /// </summary>
    Task<AiChatReply> ProbeAsync(AiChatOptions options, CancellationToken cancellationToken = default);
}
