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

namespace Launcher.Infrastructure.Accounts;

/// <summary>
/// 管理用户自行填写的 Microsoft 应用（Client ID）配置。
/// 启动器已内置一个公共应用 ID 作为兜底，正版登录默认开箱即用；
/// 这里的配置用于覆盖内置值，让登录请求归属到你自己的 Azure 应用。
/// Client ID 是公开标识符，不是账号密码，也不是机密凭据。
/// </summary>
public static class MicrosoftClientIdConfiguration
{
    public const string FileName = "microsoft-client-id";

    /// <summary>
    /// 配置文件所在的目录：%APPDATA%\CL。
    /// 与账户数据放在同一层，便于用户查找和备份。
    /// </summary>
    public static string StoreDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CL");

    /// <summary>
    /// 配置文件的完整路径：%APPDATA%\CL\microsoft-client-id。
    /// </summary>
    public static string StorePath => Path.Combine(StoreDirectory, FileName);

    /// <summary>
    /// 读取已保存的 Client ID；未配置或内容非法时返回 null。
    /// </summary>
    public static string? TryReadConfiguredValue()
    {
        try
        {
            if (!File.Exists(StorePath))
                return null;

            return TryNormalize(File.ReadAllText(StorePath), out var clientId) ? clientId : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// 保存 Client ID。传入空值等价于清除配置。
    /// </summary>
    public static bool TrySave(string? value, out string clientId, out string error)
    {
        clientId = string.Empty;
        error = string.Empty;

        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            clientId = string.Empty;
            return TryDelete(out error);
        }

        if (!TryNormalize(normalized, out clientId))
        {
            error = "invalid";
            return false;
        }

        try
        {
            Directory.CreateDirectory(StoreDirectory);
            File.WriteAllText(StorePath, clientId);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.GetType().Name;
            return false;
        }
    }

    /// <summary>
    /// 清除已保存的 Client ID。
    /// </summary>
    public static bool TryDelete(out string error)
    {
        error = string.Empty;
        try
        {
            if (File.Exists(StorePath))
                File.Delete(StorePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.GetType().Name;
            return false;
        }
    }

    /// <summary>
    /// 校验并规范化 GUID 形式的 Client ID。
    /// </summary>
    public static bool TryNormalize(string? value, out string clientId)
    {
        var normalized = value?.Trim();
        if (Guid.TryParse(normalized, out var parsed) && parsed != Guid.Empty)
        {
            clientId = parsed.ToString("D");
            return true;
        }

        clientId = string.Empty;
        return false;
    }
}
