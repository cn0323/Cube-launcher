/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * SPDX-License-Identifier: GPL-3.0-only
 */

using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Shell;

public sealed partial class LauncherBackgroundViewModel : ObservableObject
{
    private readonly ILauncherBackgroundImageCatalog catalog;
    private readonly ILauncherBackgroundImageLoader imageLoader;
    private readonly ILauncherBackgroundVideoCatalog? videoCatalog;
    private readonly IInstanceFolderService folderService;
    private readonly IStatusService statusService;
    private readonly ILogger<LauncherBackgroundViewModel> logger;
    private readonly Func<int, int> nextRandomIndex;
    private string? currentImagePath;
    private string? currentVideoPath;

    public LauncherBackgroundViewModel(
        ILauncherBackgroundImageCatalog catalog,
        ILauncherBackgroundImageLoader imageLoader,
        IInstanceFolderService folderService,
        IStatusService statusService,
        ILauncherBackgroundVideoCatalog? videoCatalog = null,
        ILogger<LauncherBackgroundViewModel>? logger = null)
        : this(catalog, imageLoader, folderService, statusService, videoCatalog, logger, Random.Shared.Next)
    {
    }

    internal LauncherBackgroundViewModel(
        ILauncherBackgroundImageCatalog catalog,
        ILauncherBackgroundImageLoader imageLoader,
        IInstanceFolderService folderService,
        IStatusService statusService,
        ILauncherBackgroundVideoCatalog? videoCatalog,
        ILogger<LauncherBackgroundViewModel>? logger,
        Func<int, int> nextRandomIndex)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));
        this.folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        this.statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        this.videoCatalog = videoCatalog;
        this.logger = logger ?? NullLogger<LauncherBackgroundViewModel>.Instance;
        this.nextRandomIndex = nextRandomIndex ?? throw new ArgumentNullException(nameof(nextRandomIndex));
    }

    [ObservableProperty]
    private ImageSource? imageSource;

    [ObservableProperty]
    private bool isActive;

    /// <summary>
    /// 当前视频背景的播放地址；为 null 表示未启用视频背景。
    /// </summary>
    [ObservableProperty]
    private Uri? videoSource;

    /// <summary>
    /// 视频背景是否正在播放。
    /// </summary>
    [ObservableProperty]
    private bool isVideoActive;

    /// <summary>
    /// 视频背景音量（0-100）。播放器按此值实时调整音量，无需重启。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoVolume))]
    [NotifyPropertyChangedFor(nameof(VideoVolumePercentText))]
    private int videoVolumePercent = LauncherDefaults.DefaultLauncherBackgroundVideoVolumePercent;

    public double VideoVolume => Math.Clamp(VideoVolumePercent, 0, 100) / 100d;

    /// <summary>
    /// 图片或视频背景是否正在显示；页面底板据此让位。
    /// </summary>
    public bool IsMediaActive => IsActive || IsVideoActive;

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(IsMediaActive));

    partial void OnIsVideoActiveChanged(bool value) => OnPropertyChanged(nameof(IsMediaActive));

    public string VideoVolumePercentText => $"{Math.Clamp(VideoVolumePercent, 0, 100)}%";

    internal string? CurrentImagePath => currentImagePath;

    internal string? CurrentVideoPath => currentVideoPath;

    public void ApplyEffect(string? backgroundEffect, bool reportFailure)
    {
        if (LauncherBackgroundEffects.IsVideo(backgroundEffect))
        {
            Deactivate();
            RefreshVideo(avoidCurrentVideo: false, reportFailure);
            return;
        }

        if (LauncherBackgroundEffects.IsImage(backgroundEffect))
        {
            DeactivateVideo();
            Refresh(avoidCurrentImage: false, reportFailure);
            return;
        }

        Deactivate();
        DeactivateVideo();
    }

    /// <summary>
    /// 切换到下一个可用的视频背景。
    /// </summary>
    public bool RefreshVideo(bool avoidCurrentVideo = true, bool reportFailure = true)
    {
        if (videoCatalog is null)
        {
            DeactivateVideo();
            if (reportFailure)
                statusService.Report(Strings.Status_NoLauncherBackgroundVideos);
            return false;
        }

        IReadOnlyList<string> candidates;
        try
        {
            candidates = videoCatalog.GetCandidatePaths();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Failed to enumerate launcher background videos. Stage=Enumerate");
            DeactivateVideo();
            if (reportFailure)
                statusService.Report(Strings.Status_ReadLauncherBackgroundVideosFailed);
            return false;
        }

        if (candidates.Count == 0)
        {
            DeactivateVideo();
            if (reportFailure)
                statusService.Report(Strings.Status_NoLauncherBackgroundVideos);
            return false;
        }

        var previousPath = currentVideoPath;
        var orderedCandidates = candidates
            .Where(path => !avoidCurrentVideo
                           || candidates.Count == 1
                           || !string.Equals(path, previousPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Shuffle(orderedCandidates);

        var selected = orderedCandidates[0];
        currentVideoPath = selected;
        VideoSource = new Uri(selected);
        IsVideoActive = true;
        logger.LogInformation(
            "Launcher background video loaded. CandidateCount={CandidateCount}",
            candidates.Count);
        return true;
    }

    public bool TryOpenVideoDirectory()
    {
        try
        {
            if (videoCatalog is null)
                return false;

            var directory = videoCatalog.EnsureDirectoryExists();
            if (folderService.TryOpen(directory))
                return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Failed to prepare launcher background video directory. Stage=OpenDirectory");
        }

        statusService.Report(Strings.Status_OpenLauncherBackgroundVideoFolderFailed);
        return false;
    }

    public bool ClearVideos()
    {
        try
        {
            if (videoCatalog is null)
                return false;

            videoCatalog.ClearVideos();
            DeactivateVideo();
            logger.LogInformation("Launcher background videos cleared.");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeactivateVideo();
            logger.LogWarning(
                exception,
                "Failed to clear launcher background videos. Stage=Clear");
            statusService.Report(Strings.Status_ClearLauncherBackgroundVideosFailed);
            return false;
        }
    }

    public void DeactivateVideo()
    {
        IsVideoActive = false;
        VideoSource = null;
        currentVideoPath = null;
    }

    public bool Refresh(bool avoidCurrentImage = true, bool reportFailure = true)
    {
        IReadOnlyList<string> candidates;
        try
        {
            candidates = catalog.GetCandidatePaths();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Failed to enumerate launcher background images. Stage=Enumerate");
            Deactivate();
            if (reportFailure)
                statusService.Report(Strings.Status_ReadLauncherBackgroundImagesFailed);
            return false;
        }

        logger.LogDebug(
            "Launcher background image candidates enumerated. CandidateCount={CandidateCount}",
            candidates.Count);
        if (candidates.Count == 0)
        {
            Deactivate();
            if (reportFailure)
                statusService.Report(Strings.Status_NoLauncherBackgroundImages);
            return false;
        }

        var previousPath = currentImagePath;
        var canKeepCurrent = ImageSource is not null
                             && previousPath is not null
                             && candidates.Contains(previousPath, StringComparer.OrdinalIgnoreCase);
        var orderedCandidates = candidates
            .Where(path => !avoidCurrentImage
                           || candidates.Count == 1
                           || !string.Equals(path, previousPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Shuffle(orderedCandidates);

        for (var index = 0; index < orderedCandidates.Count; index++)
        {
            try
            {
                var source = imageLoader.Load(orderedCandidates[index]);
                currentImagePath = orderedCandidates[index];
                ImageSource = source;
                IsActive = true;
                logger.LogInformation(
                    "Launcher background image loaded. CandidateCount={CandidateCount} Attempt={Attempt}",
                    candidates.Count,
                    index + 1);
                return true;
            }
            catch (Exception exception) when (
                exception is IOException
                or FileFormatException
                or UnauthorizedAccessException
                or NotSupportedException
                or ArgumentException
                or InvalidOperationException
                or COMException)
            {
                logger.LogDebug(
                    exception,
                    "Failed to decode launcher background image. Stage=Decode Attempt={Attempt} CandidateCount={CandidateCount}",
                    index + 1,
                    candidates.Count);
            }
        }

        if (!canKeepCurrent)
            Deactivate();
        if (reportFailure)
        {
            statusService.Report(canKeepCurrent
                ? Strings.Status_NoOtherLauncherBackgroundImages
                : Strings.Status_NoLauncherBackgroundImages);
        }
        return false;
    }

    public bool TryOpenDirectory()
    {
        try
        {
            var directory = catalog.EnsureDirectoryExists();
            if (folderService.TryOpen(directory))
                return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Failed to prepare launcher background image directory. Stage=OpenDirectory");
        }

        statusService.Report(Strings.Status_OpenLauncherBackgroundImageFolderFailed);
        return false;
    }

    public bool ClearImages()
    {
        try
        {
            catalog.ClearImages();
            Deactivate();
            logger.LogInformation("Launcher background images cleared.");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Deactivate();
            logger.LogWarning(
                exception,
                "Failed to clear launcher background images. Stage=Clear");
            statusService.Report(Strings.Status_ClearLauncherBackgroundImagesFailed);
            return false;
        }
    }

    public void Deactivate()
    {
        IsActive = false;
        ImageSource = null;
        currentImagePath = null;
    }

    private void Shuffle(IList<string> paths)
    {
        for (var index = paths.Count - 1; index > 0; index--)
        {
            var swapIndex = nextRandomIndex(index + 1);
            if (swapIndex < 0 || swapIndex > index)
                throw new InvalidOperationException("The background image random index was outside the requested range.");
            (paths[index], paths[swapIndex]) = (paths[swapIndex], paths[index]);
        }
    }

}
