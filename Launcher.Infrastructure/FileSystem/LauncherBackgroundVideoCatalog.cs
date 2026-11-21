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
using Launcher.Application.Services;

namespace Launcher.Infrastructure.FileSystem;

/// <summary>
/// 视频背景目录：数据目录下的 videos 子目录，支持 Windows Media Foundation 能解码的常见容器。
/// </summary>
public sealed class LauncherBackgroundVideoCatalog : ILauncherBackgroundVideoCatalog
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".m4v",
        ".wmv",
        ".avi",
        ".webm",
        ".mov",
        ".mkv"
    };

    private readonly IUserFileDeletionService userFileDeletionService;

    public LauncherBackgroundVideoCatalog(
        LauncherPathProvider pathProvider,
        IUserFileDeletionService? userFileDeletionService = null)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        DirectoryPath = Path.Combine(pathProvider.DefaultDataDirectory, "videos");
        this.userFileDeletionService = userFileDeletionService ?? new UserFileDeletionService();
    }

    public string DirectoryPath { get; }

    public string EnsureDirectoryExists()
    {
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public IReadOnlyList<string> GetCandidatePaths()
    {
        var directory = EnsureDirectoryExists();
        return Directory
            .EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void ClearVideos()
    {
        foreach (var videoPath in GetCandidatePaths())
            userFileDeletionService.DeleteFile(videoPath);
    }
}
