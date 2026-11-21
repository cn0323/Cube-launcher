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

using Launcher.App.ViewModels.Home;
using Launcher.App.ViewModels.Settings;
using Launcher.Application.Services;
using Launcher.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.Services;

public sealed class HomePageViewModelFactory : IHomePageViewModelFactory
{
    private readonly ILaunchService launchService;
    private readonly IStatusService statusService;
    private readonly IFloatingMessageService floatingMessageService;
    private readonly IWindowService windowService;
    private readonly IUiDispatcher uiDispatcher;
    private readonly IAccountDialogService accountDialogService;
    private readonly SettingsPersistenceCoordinator? settingsPersistence;
    private readonly ILogger<HomePageViewModel> logger;

    public HomePageViewModelFactory(
        ILaunchService launchService,
        IStatusService statusService,
        IFloatingMessageService floatingMessageService,
        IWindowService windowService,
        IUiDispatcher uiDispatcher,
        IAccountDialogService accountDialogService,
        ILogger<HomePageViewModel>? logger = null,
        SettingsPersistenceCoordinator? settingsPersistence = null)
    {
        this.launchService = launchService;
        this.statusService = statusService;
        this.floatingMessageService = floatingMessageService;
        this.windowService = windowService;
        this.uiDispatcher = uiDispatcher;
        this.accountDialogService = accountDialogService;
        this.settingsPersistence = settingsPersistence;
        this.logger = logger ?? NullLogger<HomePageViewModel>.Instance;
    }

    public HomePageViewModel Create(
        AccountPageViewModel accountPage,
        Action<double> reportProgressPercent,
        Func<GameInstance, Task<bool>> selectLaunchInstance,
        Func<bool, Task<bool>> setLaunchMenuPinned,
        Func<GameInstance?, Task> openGameSettingsForInstance,
        Action? openAccountPage = null,
        Action<string>? navigateToPage = null)
    {
        return new HomePageViewModel(
            launchService,
            accountPage,
            statusService,
            floatingMessageService,
            windowService,
            uiDispatcher,
            reportProgressPercent,
            selectLaunchInstance,
            setLaunchMenuPinned,
            openGameSettingsForInstance,
            logger,
            accountDialogService,
            settingsPersistence,
            openAccountPage,
            navigateToPage);
    }
}
