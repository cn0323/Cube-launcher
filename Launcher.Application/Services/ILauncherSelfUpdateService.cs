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

public interface ILauncherSelfUpdateService
{
    /// <summary>
    /// 下载并启动更新。进度以 0~100 的百分比回报，界面据此显示启动器内的下载进度。
    /// </summary>
    Task<LauncherSelfUpdateStartResult> StartUpdateAsync(
        LauncherUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
