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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.Application;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Shell;

public sealed partial class UserAgreementDialogViewModel : ObservableObject
{
    private readonly ISettingsService settingsService;
    private readonly UserAgreementViewerViewModel agreementViewer;
    private readonly IApplicationExitService applicationExitService;
    private readonly IStatusService statusService;
    private readonly IFloatingMessageService floatingMessageService;
    private readonly ILogger<UserAgreementDialogViewModel> logger;
    private readonly TaskCompletionSource<bool> decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private LauncherSettings? settings;

    [ObservableProperty]
    private bool isOpen;

    public UserAgreementDialogViewModel(
        ISettingsService settingsService,
        UserAgreementViewerViewModel agreementViewer,
        IApplicationExitService applicationExitService,
        IStatusService statusService,
        IFloatingMessageService floatingMessageService,
        ILogger<UserAgreementDialogViewModel>? logger = null)
    {
        this.settingsService = settingsService;
        this.agreementViewer = agreementViewer;
        this.applicationExitService = applicationExitService;
        this.statusService = statusService;
        this.floatingMessageService = floatingMessageService;
        this.logger = logger ?? NullLogger<UserAgreementDialogViewModel>.Instance;
    }

    public void Prime(LauncherSettings launcherSettings)
    {
        ArgumentNullException.ThrowIfNull(launcherSettings);
        settings = launcherSettings;
        IsOpen = !launcherSettings.HasAcceptedUserAgreement;

        if (!IsOpen)
            decision.TrySetResult(true);
    }

    public Task<bool> WaitForDecisionAsync() => decision.Task;

    [RelayCommand]
    private async Task AgreeAsync()
    {
        if (settings is null || settings.HasAcceptedUserAgreement)
            return;

        settings.HasAcceptedUserAgreement = true;
        try
        {
            await settingsService.UpdateAsync(latest => latest.HasAcceptedUserAgreement = true);
            IsOpen = false;
            decision.TrySetResult(true);
            logger.LogInformation("User agreement accepted and persisted.");
        }
        catch (Exception exception)
        {
            settings.HasAcceptedUserAgreement = false;
            logger.LogWarning(exception, "Failed to persist user agreement acceptance.");
            ReportFailure(Strings.Status_UserAgreementSaveFailed);
        }
    }

    [RelayCommand]
    private void DisagreeAndExit()
    {
        logger.LogInformation("User agreement declined; launcher exit requested.");
        decision.TrySetResult(false);
        applicationExitService.Shutdown();
    }

    [RelayCommand]
    private void OpenAgreement()
    {
        // 协议全文随程序内置，直接在启动器内展示，不再跳转外部网页。
        logger.LogInformation("User agreement viewer opened.");
        agreementViewer.Show();
    }

    private void ReportFailure(string message)
    {
        statusService.Report(message);
        floatingMessageService.Show(message);
    }
}
