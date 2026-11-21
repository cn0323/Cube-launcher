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

namespace Launcher.Application.Services;

public sealed record LauncherUpdateCheckResult(
    bool IsUpdateAvailable,
    LauncherUpdateInfo? Update,
    string CurrentVersion,
    bool IsFailed,
    string? ErrorMessage,
    bool HasPublishedVersion = true)
{
    public static LauncherUpdateCheckResult Latest(string currentVersion)
    {
        return new LauncherUpdateCheckResult(false, null, currentVersion, false, null);
    }

    public static LauncherUpdateCheckResult Available(string currentVersion, LauncherUpdateInfo update)
    {
        return new LauncherUpdateCheckResult(true, update, currentVersion, false, null);
    }

    public static LauncherUpdateCheckResult Failed(string currentVersion, string? errorMessage = null)
    {
        return new LauncherUpdateCheckResult(false, null, currentVersion, true, errorMessage);
    }

    /// <summary>
    /// 发布端（GitHub Releases / Tags）没有任何版本记录。
    /// 这与“已是最新版本”不同：前者是维护者还没发布，后者是确实没有更新的版本。
    /// </summary>
    public static LauncherUpdateCheckResult NoPublishedVersion(string currentVersion)
    {
        return new LauncherUpdateCheckResult(false, null, currentVersion, false, null, false);
    }
}
