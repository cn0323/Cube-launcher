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

using System.IO;
using System.Text;

namespace Launcher.App.Services;

/// <summary>
/// 提供内置的用户协议全文。
/// 协议正文取自仓库根目录的 USER_AGREEMENT.md，构建时嵌入程序集，
/// 因此点击“用户协议”即可在启动器内直接阅读，无需联网，也不会跳转到浏览器。
/// </summary>
public static class UserAgreementContent
{
    internal const string ResourceName = "Launcher.App.Resources.UserAgreement.md";

    private static readonly Lazy<string> LazyMarkdown = new(
        Load,
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 获取用户协议的 Markdown 原文。
    /// </summary>
    public static string Markdown => LazyMarkdown.Value;

    private static string Load()
    {
        using var stream = typeof(UserAgreementContent).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
            return string.Empty;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
