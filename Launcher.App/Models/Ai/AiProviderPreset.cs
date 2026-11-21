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

namespace Launcher.App.Models.Ai;

/// <summary>
/// AI 服务的接入预设。接口地址和默认模型名属于技术参数，品牌名不需要翻译。
/// 用户选择预设只是把这两项填进输入框，实际请求仍以输入框内容为准。
/// </summary>
public sealed record AiProviderPreset(string Name, string Endpoint, string Model)
{
    public bool IsCustom => string.IsNullOrEmpty(Endpoint);

    public override string ToString() => Name;

    /// <summary>
    /// 常用服务商。全部走 OpenAI 兼容的 chat/completions，切换时只改 Base URL 和模型名。
    /// </summary>
    public static IReadOnlyList<AiProviderPreset> All { get; } =
    [
        new("DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
        new("通义千问", "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-plus"),
        new("Kimi", "https://api.moonshot.cn/v1", "moonshot-v1-8k"),
        new("智谱 GLM", "https://open.bigmodel.cn/api/paas/v4", "glm-4-flash"),
        new("硅基流动", "https://api.siliconflow.cn/v1", "Qwen/Qwen2.5-7B-Instruct"),
        new("OpenAI", "https://api.openai.com/v1", "gpt-4o-mini"),
        new("Ollama 本地", "http://localhost:11434/v1", "qwen2.5:7b"),
        new("自定义", string.Empty, string.Empty)
    ];
}
