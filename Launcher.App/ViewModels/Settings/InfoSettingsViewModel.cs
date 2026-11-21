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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Models;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Shell;
using Launcher.Application;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Settings;

/// <summary>
/// 展示启动器版本与参考项目，并协调手动/启动时更新检查及自更新确认流程。
/// </summary>
public sealed partial class InfoSettingsViewModel : SettingsSectionViewModelBase
{
    // 检查更新与执行更新是两个独立阶段：前者可静默，后者必须经过用户确认。
    private readonly IStatusService statusService;
    private readonly IFloatingMessageService floatingMessageService;
    private readonly IExternalLinkService externalLinkService;
    private readonly ILauncherUpdateService launcherUpdateService;
    private readonly ILauncherSelfUpdateService launcherSelfUpdateService;
    private readonly IApplicationExitService applicationExitService;
    private readonly UserAgreementViewerViewModel agreementViewer;
    private readonly ILogger<InfoSettingsViewModel> logger;
    private LauncherUpdateInfo? availableUpdate;
    private bool isUpdateCheckRunning;
    private bool isUpdateDialogPreview;

    internal InfoSettingsViewModel(
        SettingsPersistenceCoordinator persistence,
        IStatusService statusService,
        IFloatingMessageService floatingMessageService,
        IExternalLinkService externalLinkService,
        ILauncherUpdateService launcherUpdateService,
        ILauncherSelfUpdateService launcherSelfUpdateService,
        IApplicationExitService applicationExitService,
        IInfoReferenceProjectCatalog referenceProjectCatalog,
        UserAgreementViewerViewModel agreementViewer,
        ILogger<InfoSettingsViewModel>? logger = null)
        : base(persistence)
    {
        this.statusService = statusService;
        this.floatingMessageService = floatingMessageService;
        this.externalLinkService = externalLinkService;
        this.launcherUpdateService = launcherUpdateService;
        this.launcherSelfUpdateService = launcherSelfUpdateService;
        this.applicationExitService = applicationExitService;
        this.agreementViewer = agreementViewer;
        this.logger = logger ?? NullLogger<InfoSettingsViewModel>.Instance;
        LauncherVersionText = ResolveLauncherVersion();
        ReferenceProjects = referenceProjectCatalog.GetProjects();
        // 更新通道只保留正式版：预发布版本不再通过自动更新分发。
        UpdateChannelOptions =
        [
            new(LauncherUpdateChannel.Release, Strings.Settings_UpdateChannelReleaseTitle)
        ];
        selectedUpdateChannelOption = UpdateChannelOptions[0];
    }

    public string LauncherVersionText { get; }

    public IReadOnlyList<InfoReferenceProjectItem> ReferenceProjects { get; }

    public ObservableCollection<SettingsUpdateChannelOption> UpdateChannelOptions { get; }

    [ObservableProperty]
    private SettingsUpdateChannelOption? selectedUpdateChannelOption;

    [ObservableProperty]
    private bool isUpdateAvailableDialogOpen;

    [ObservableProperty]
    private string updateDialogVersionText = string.Empty;

    [ObservableProperty]
    private string updateDialogMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateReleaseNotes))]
    private string updateReleaseNotesMarkdown = string.Empty;

    [ObservableProperty]
    private bool isCheckingUpdates;

    [ObservableProperty]
    private bool isStartingUpdate;

    [ObservableProperty]
    private double updateProgressPercent;

    /// <summary>下载阶段的进度提示，只有真正开始下载后才显示。</summary>
    public bool IsUpdateProgressVisible => IsStartingUpdate;

    public string UpdateProgressText => string.Format(
        Strings.Dialog_UpdateDownloadingFormat,
        (int)Math.Round(UpdateProgressPercent));

    /// <summary>
    /// 读取更新偏好，不在此处触发网络请求。
    /// </summary>
    public void Load(LauncherSettings settings)
    {
        // 这里只读取更新偏好和当前版本，不在设置页加载时自动发起网络请求。
        LoadState(() =>
        {
            SelectedUpdateChannelOption = UpdateChannelOptions.FirstOrDefault(option =>
                option.Channel == settings.UpdateChannel) ?? UpdateChannelOptions[0];
        });
    }

    public string CheckUpdatesButtonText => IsCheckingUpdates
        ? Strings.Status_CheckingUpdates
        : Strings.Settings_CheckUpdatesButton;

    /// <summary>
    /// 发布页提供了新版但资产不能自动安装（缺少 sha256 或没有上传 exe），
    /// 此时确认按钮改为引导用户去发布页手动下载，而不是点了报“找不到安装包”。
    /// </summary>
    public bool IsUpdateAutoInstallAvailable => availableUpdate?.CanAutoInstall ?? false;

    public string ConfirmUpdateButtonText
    {
        get
        {
            if (IsStartingUpdate)
                return Strings.Status_DownloadingLauncherUpdate;
            return IsUpdateAutoInstallAvailable
                ? Strings.Dialog_UpdateButton
                : Strings.Dialog_UpdateGotoDownloadButton;
        }
    }

    public bool HasUpdateReleaseNotes => !string.IsNullOrWhiteSpace(UpdateReleaseNotesMarkdown);

#if DEBUG
    public bool IsUpdateDialogPreviewAvailable => true;
#else
    public bool IsUpdateDialogPreviewAvailable => false;
#endif
}
