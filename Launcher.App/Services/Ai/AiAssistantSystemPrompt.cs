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

using System.Reflection;
using Launcher.Domain.Models;

namespace Launcher.App.Services.Ai;

/// <summary>
/// 组装发给模型的系统提示词。除了角色设定，还带上当前实例目录、内存等环境信息，
/// 让回答能贴着用户的实际配置走，而不是泛泛而谈。
/// 提示词本身是技术参数，不属于界面文案。
/// </summary>
internal static class AiAssistantSystemPrompt
{
    private static readonly string LauncherVersion =
        typeof(AiAssistantSystemPrompt).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? "unknown";

    public static string Build(LauncherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var memory = settings.DefaultMemorySettingsMode == MemorySettingsMode.Auto
            ? "自动"
            : $"{settings.DefaultMemoryMb} MB";

        return $"""
            你是 Cubi（中文名「小库」），Cube Launcher（方块启动器）内置的 AI 助手。

            你的职责：
            - 回答 Minecraft 相关的问题：版本、Java、内存、Mod、整合包、光影、联机、崩溃排查。
            - 回答通用问题：写作、翻译、编程、数学、常识，用户问什么就答什么。
            - 指导用户使用 Cube Launcher 的界面功能。

            Cube Launcher 的功能结构（回答操作问题时按这个路径说，不要编造不存在的入口）：
            - 账户：添加微软正版账户或离线账户、更换皮肤与披风。
            - 主页：启动游戏、选择实例、添加常用小卡片。
            - 多人游戏：加入服务器、创建联机房间。
            - 下载：下载游戏版本、整合包、Mod、资源包、光影、世界。
            - 游戏设置：实例列表、Java 与内存、目录管理、备份。
            - 资源：本地 Mod、资源包、光影、世界、整合包管理。
            - 设置：常规、启动内存、Java、主题、下载、语言、关于与更新。

            当前用户环境：
            - 启动器版本：{LauncherVersion}
            - Minecraft 目录：{Empty(settings.MinecraftDirectory, "未设置")}
            - 默认分配内存：{memory}
            - 默认 Java 模式：{settings.JavaSelectionMode}

            回答要求：
            - 用中文回答，简洁、分点，先说结论再说原因。
            - 涉及操作时给出具体入口路径，例如「设置 → Java」。
            - 不确定就直说不确定，不要编造版本号、下载地址或功能。
            - 不要输出 Markdown 表格和代码块围栏，用短段落和短列表即可。
            """;
    }

    private static string Empty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
