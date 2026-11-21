/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 * SPDX-License-Identifier: GPL-3.0-only
 */

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Resources;
using Launcher.App.Services;
using Launcher.App.ViewModels.Account;
using Launcher.Application;
using Launcher.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.App.ViewModels.Multiplayer;

public sealed partial class MultiplayerPageViewModel : ObservableObject
{
    private readonly AccountPageViewModel? accountPage;
    private readonly IMultiplayerLobbyService lobbyService;
    private readonly IClipboardService clipboardService;
    private readonly IUiDispatcher uiDispatcher;
    private readonly IStatusService statusService;
    private readonly IFloatingMessageService floatingMessageService;
    private readonly IExternalLinkService? externalLinkService;
    private readonly ILogger<MultiplayerPageViewModel> logger;

    [ObservableProperty]
    private MultiplayerSectionItem? selectedSection;

    /// <summary>
    /// 陶瓦联机内的当前操作模式（创建房间 / 加入房间）。
    /// 只影响陶瓦分区内的表单，红石分区不使用该状态。
    /// </summary>
    [ObservableProperty]
    private MultiplayerTerracottaMode terracottaMode = MultiplayerTerracottaMode.Create;

    [ObservableProperty]
    private MultiplayerCreateLobbyStep createLobbyStep;

    [ObservableProperty]
    private string lobbyOwnerName = Strings.Multiplayer_LobbyOwnerPlaceholder;

    [ObservableProperty]
    private string roomCode = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinLobbyCommand))]
    private string joinRoomCode = string.Empty;

    [ObservableProperty]
    private bool isLobbyHost;

    [ObservableProperty]
    private bool isLeaveLobbyDialogOpen;

    [ObservableProperty]
    private bool isLobbySectionSwitchBlockedDialogOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateLobbyCommand))]
    private bool isCreatingLobby;

    [ObservableProperty]
    private bool isLanWorldDetectionDialogOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PasteRoomCodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(JoinLobbyCommand))]
    private bool isJoiningLobby;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestLeaveLobbyCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmLeaveLobbyCommand))]
    private bool isStoppingLobby;

    [ObservableProperty]
    private string joinLobbyStatus = string.Empty;

    public MultiplayerPageViewModel(
        IMultiplayerLobbyService lobbyService,
        IClipboardService clipboardService,
        IUiDispatcher uiDispatcher,
        IStatusService statusService,
        IFloatingMessageService floatingMessageService,
        AccountPageViewModel? accountPage = null,
        IExternalLinkService? externalLinkService = null,
        ILogger<MultiplayerPageViewModel>? logger = null)
    {
        this.lobbyService = lobbyService;
        this.clipboardService = clipboardService;
        this.uiDispatcher = uiDispatcher;
        this.statusService = statusService;
        this.floatingMessageService = floatingMessageService;
        this.accountPage = accountPage;
        this.externalLinkService = externalLinkService;
        this.logger = logger ?? NullLogger<MultiplayerPageViewModel>.Instance;
        Sections =
        [
            new(MultiplayerPageSection.Terracotta, Strings.Multiplayer_SectionTerracotta, "multiple_player/multi_create"),
            new(MultiplayerPageSection.Redstone, Strings.Multiplayer_SectionRedstone, "multiple_player/multi_redstone")
        ];
        SelectedSection = Sections[0];
        lobbyService.SnapshotChanged += OnLobbySnapshotChanged;
        lobbyService.Stopped += OnLobbyStopped;
    }

    public ObservableCollection<MultiplayerSectionItem> Sections { get; }

    public ObservableCollection<MultiplayerLobbyPlayerItem> LobbyPlayers { get; } = [];

    public string SectionTitle => IsLobbyStep
        ? LobbyTitle
        : SelectedSection?.Title ?? Strings.Multiplayer_SectionCreateLobby;

    /// <summary>
    /// 陶瓦联机是联机页的第一个分区，创建房间与加入房间都在其中切换。
    /// </summary>
    public bool IsTerracottaSection => SelectedSection?.Section is MultiplayerPageSection.Terracotta;

    public bool IsCreateLobbySection => IsTerracottaSection
        && TerracottaMode is MultiplayerTerracottaMode.Create;

    public bool IsJoinLobbySection => IsTerracottaSection
        && TerracottaMode is MultiplayerTerracottaMode.Join;

    public bool IsRedstoneSection => SelectedSection?.Section is MultiplayerPageSection.Redstone;

    /// <summary>
    /// 只在陶瓦分区的表单阶段显示「创建房间 / 加入房间」切换；进入房间后收起。
    /// </summary>
    public bool IsTerracottaModeSwitchVisible => IsTerracottaSection && !IsLobbyStep;

    public bool IsTerracottaCreateMode => TerracottaMode is MultiplayerTerracottaMode.Create;

    public bool IsTerracottaJoinMode => TerracottaMode is MultiplayerTerracottaMode.Join;

    public bool IsLobbyStep => CreateLobbyStep is MultiplayerCreateLobbyStep.Lobby;

    public string LobbyTitle => string.Format(Strings.Multiplayer_LobbyTitleFormat, LobbyOwnerName);

    public string JoinLobbyButtonText => IsJoiningLobby
        ? Strings.Multiplayer_Join_Joining
        : Strings.Multiplayer_SectionJoinLobby;

    public bool HasJoinLobbyStatus => !string.IsNullOrWhiteSpace(JoinLobbyStatus);

    public string LeaveLobbyButtonText => IsLobbyHost
        ? Strings.Multiplayer_LobbyLeaveAndDisbandButton
        : Strings.Multiplayer_LobbyLeaveButton;

    public string LeaveLobbyDialogTitle => IsLobbyHost
        ? Strings.Dialog_MultiplayerLeaveLobbyTitle
        : Strings.Dialog_MultiplayerLeaveJoinedLobbyTitle;

    public string LeaveLobbyDialogMessage => IsLobbyHost
        ? Strings.Dialog_MultiplayerLeaveLobbyMessage
        : Strings.Dialog_MultiplayerLeaveJoinedLobbyMessage;

    public string LeaveLobbyConfirmButtonText => IsLobbyHost
        ? Strings.Dialog_MultiplayerLeaveLobbyConfirmButton
        : Strings.Dialog_MultiplayerLeaveJoinedLobbyConfirmButton;

    private bool CanCreateLobby => !IsCreatingLobby;

    private bool CanPasteRoomCode => !IsJoiningLobby;

    private bool CanJoinLobby => !string.IsNullOrWhiteSpace(JoinRoomCode)
        && !IsJoiningLobby;

    private bool CanRequestLeaveLobby => IsLobbyStep && !IsStoppingLobby;

    private bool CanConfirmLeaveLobby => IsLobbyStep && !IsStoppingLobby;

    private bool CanCopyRoomCode => !string.IsNullOrWhiteSpace(RoomCode);

    private bool CanCancelLobbyDetection => IsCreatingLobby;

    [RelayCommand]
    private void SelectSection(MultiplayerSectionItem? section)
    {
        if (section is null || section.Section == SelectedSection?.Section)
            return;

        if (IsLobbyStep)
        {
            IsLobbySectionSwitchBlockedDialogOpen = true;
            return;
        }

        SelectedSection = section;
    }

    [RelayCommand]
    private void CloseLobbySectionSwitchBlockedDialog()
    {
        IsLobbySectionSwitchBlockedDialogOpen = false;
    }

    /// <summary>
    /// 在陶瓦联机内切换「创建房间 / 加入房间」；已进入房间时先提示先退出房间。
    /// </summary>
    [RelayCommand]
    private void SelectTerracottaMode(MultiplayerTerracottaMode mode)
    {
        if (mode == TerracottaMode)
            return;

        if (IsLobbyStep)
        {
            IsLobbySectionSwitchBlockedDialogOpen = true;
            return;
        }

        TerracottaMode = mode;
    }

    [RelayCommand(CanExecute = nameof(CanCreateLobby))]
    private async Task CreateLobbyAsync(CancellationToken cancellationToken)
    {
        IsCreatingLobby = true;
        IsLanWorldDetectionDialogOpen = true;
        try
        {
            var hostName = accountPage?.SelectedAccount?.DisplayName
                ?? Strings.Multiplayer_LobbyOwnerPlaceholder;
            var snapshot = await lobbyService.CreateHostAsync(hostName, cancellationToken);
            IsLobbyHost = true;
            ApplyLobbySnapshot(snapshot);
            CreateLobbyStep = MultiplayerCreateLobbyStep.Lobby;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (MultiplayerLobbyCreationException exception)
        {
            logger.LogWarning(
                "Failed to create multiplayer lobby. Failure={Failure} ErrorType={ErrorType}",
                exception.Failure, exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            ReportFailure(MapCreationFailure(exception.Failure));
        }
        catch (Exception exception)
        {
            logger.LogWarning("Failed to create multiplayer lobby. ErrorType={ErrorType}", exception.GetType().Name);
            ReportFailure(Strings.Multiplayer_Create_LobbyFailed);
        }
        finally
        {
            IsLanWorldDetectionDialogOpen = false;
            IsCreatingLobby = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelLobbyDetection))]
    private void CancelLobbyDetection()
    {
        IsLanWorldDetectionDialogOpen = false;
        CreateLobbyCommand.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanPasteRoomCode))]
    private async Task PasteRoomCodeAsync(CancellationToken cancellationToken)
    {
        string? clipboardText;
        try
        {
            clipboardText = await clipboardService.GetTextAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to read a Terracotta room code from the clipboard.");
            ReportJoinFailure(Strings.Multiplayer_Join_ClipboardEmpty);
            return;
        }

        var normalized = clipboardText?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            ReportJoinFailure(Strings.Multiplayer_Join_ClipboardEmpty);
            return;
        }

        JoinRoomCode = normalized;
        JoinLobbyStatus = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanJoinLobby))]
    private async Task JoinLobbyAsync(CancellationToken cancellationToken)
    {
        var normalizedRoomCode = JoinRoomCode.Trim();
        if (normalizedRoomCode.Length == 0)
            return;

        IsJoiningLobby = true;
        JoinLobbyStatus = string.Empty;
        try
        {
            var playerName = accountPage?.SelectedAccount?.DisplayName
                ?? Strings.Multiplayer_LobbyOwnerPlaceholder;
            var snapshot = await lobbyService.JoinAsync(
                normalizedRoomCode,
                playerName,
                cancellationToken);
            IsLobbyHost = false;
            JoinRoomCode = snapshot.RoomCode;
            ApplyLobbySnapshot(snapshot);
            CreateLobbyStep = MultiplayerCreateLobbyStep.Lobby;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (MultiplayerLobbyCreationException exception)
        {
            logger.LogWarning(
                "Failed to join multiplayer lobby. Failure={Failure} ErrorType={ErrorType}",
                exception.Failure, exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            ReportJoinFailure(MapJoinFailure(exception.Failure));
        }
        catch (Exception exception)
        {
            logger.LogWarning("Failed to join multiplayer lobby. ErrorType={ErrorType}", exception.GetType().Name);
            ReportJoinFailure(Strings.Multiplayer_Join_Failed);
        }
        finally
        {
            IsJoiningLobby = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRequestLeaveLobby))]
    private void RequestLeaveLobby()
    {
        IsLeaveLobbyDialogOpen = true;
    }

    [RelayCommand]
    private void CancelLeaveLobby()
    {
        IsLeaveLobbyDialogOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmLeaveLobby))]
    private async Task ConfirmLeaveLobbyAsync(CancellationToken cancellationToken)
    {
        IsLeaveLobbyDialogOpen = false;
        IsStoppingLobby = true;
        try
        {
            await lobbyService.StopAsync(cancellationToken);
            ResetLobbyView();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to stop multiplayer lobby cleanly.");
            ReportFailure(IsLobbyHost
                ? Strings.Multiplayer_LobbyDisbandFailed
                : Strings.Multiplayer_LobbyLeaveFailed);
        }
        finally
        {
            IsStoppingLobby = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyRoomCode))]
    private async Task CopyRoomCodeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await clipboardService.CopyTextAsync(RoomCode, cancellationToken))
            {
                statusService.Report(Strings.Multiplayer_LobbyRoomCodeCopied);
                floatingMessageService.Show(Strings.Multiplayer_LobbyRoomCodeCopied);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to copy the multiplayer room code.");
        }

        statusService.Report(Strings.Multiplayer_LobbyRoomCodeCopyFailed);
        floatingMessageService.Show(Strings.Multiplayer_LobbyRoomCodeCopyFailed);
    }

    [RelayCommand]
    private void OpenTerracottaProject()
    {
        try
        {
            if (externalLinkService?.TryOpen(TerracottaAgreementDialogViewModel.TerracottaProjectUrl) is true)
                return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to open the Terracotta project page from the multiplayer page.");
            ReportExternalLinkFailure();
            return;
        }

        logger.LogWarning("Failed to open the Terracotta project page from the multiplayer page.");
        ReportExternalLinkFailure();
    }

    /// <summary>
    /// 打开红石联机官网：它以中继方式把本机端口映射成公网地址，不需要安装模组。
    /// </summary>
    [RelayCommand]
    private void OpenRedstoneSite()
    {
        try
        {
            if (externalLinkService?.TryOpen(LauncherProjectLinks.RedstoneMultiplayerUrl) is true)
                return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to open the Redstone multiplayer site.");
            ReportRedstoneLinkFailure();
            return;
        }

        ReportRedstoneLinkFailure();
    }

    [RelayCommand]
    private async Task CopyRedstoneSiteAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await clipboardService.CopyTextAsync(
                    LauncherProjectLinks.RedstoneMultiplayerUrl,
                    cancellationToken))
            {
                statusService.Report(Strings.Multiplayer_RedstoneSiteCopied);
                floatingMessageService.Show(Strings.Multiplayer_RedstoneSiteCopied);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to copy the Redstone multiplayer site address.");
        }

        statusService.Report(Strings.Multiplayer_RedstoneSiteCopyFailed);
        floatingMessageService.Show(Strings.Multiplayer_RedstoneSiteCopyFailed);
    }

    private void ReportRedstoneLinkFailure()
    {
        statusService.Report(Strings.Multiplayer_RedstoneOpenSiteFailed);
        floatingMessageService.Show(Strings.Multiplayer_RedstoneOpenSiteFailed);
    }

    private void OnLobbySnapshotChanged(MultiplayerLobbySnapshot snapshot)
    {
        uiDispatcher.Post(() => ApplyLobbySnapshot(snapshot));
    }

    private void OnLobbyStopped(MultiplayerLobbyStopped stopped)
    {
        uiDispatcher.Post(() =>
        {
            ResetLobbyView();
            var message = stopped.Reason switch
            {
                MultiplayerLobbyStopReason.MinecraftWorldClosed => Strings.Multiplayer_LobbyWorldClosed,
                MultiplayerLobbyStopReason.TerracottaExited => Strings.Multiplayer_LobbyTerracottaExited,
                MultiplayerLobbyStopReason.TerracottaServiceFailed => Strings.Multiplayer_LobbyTerracottaServiceFailed,
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(message))
                ReportFailure(message);
        });
    }

    private void ApplyLobbySnapshot(MultiplayerLobbySnapshot snapshot)
    {
        RoomCode = snapshot.RoomCode;
        LobbyOwnerName = snapshot.Players
            .FirstOrDefault(player => player.Kind is MultiplayerLobbyPlayerKind.Host)?.DisplayName
            ?? Strings.Multiplayer_LobbyOwnerPlaceholder;
        LobbyPlayers.Clear();
        for (var index = 0; index < snapshot.Players.Count; index++)
        {
            var player = snapshot.Players[index];
            LobbyPlayers.Add(new MultiplayerLobbyPlayerItem(
                player.DisplayName,
                player.Vendor,
                player.LatencyMilliseconds is { } latency
                    ? string.Format(Strings.Multiplayer_LobbyLatencyFormat, latency)
                    : Strings.Multiplayer_LobbyLatencyUnknown,
                player.Kind is MultiplayerLobbyPlayerKind.Host
                    ? Strings.Multiplayer_LobbyPlayerRoleHost
                    : Strings.Multiplayer_LobbyPlayerRolePlayer,
                player.Kind is MultiplayerLobbyPlayerKind.Host,
                player.IsLocal,
                index == 0,
                index == snapshot.Players.Count - 1));
        }
    }

    private void ResetLobbyView()
    {
        CreateLobbyStep = MultiplayerCreateLobbyStep.Setup;
        IsLanWorldDetectionDialogOpen = false;
        IsLeaveLobbyDialogOpen = false;
        IsLobbySectionSwitchBlockedDialogOpen = false;
        IsLobbyHost = false;
        RoomCode = string.Empty;
        LobbyPlayers.Clear();
    }

    private void ReportFailure(string message)
    {
        if (IsJoinLobbySection)
            JoinLobbyStatus = message;
        statusService.Report(message);
        floatingMessageService.Show(message);
    }

    private void ReportJoinFailure(string message)
    {
        JoinLobbyStatus = message;
        statusService.Report(message);
        floatingMessageService.Show(message);
    }

    private void ReportExternalLinkFailure()
    {
        statusService.Report(Strings.Status_OpenTerracottaProjectFailed);
        floatingMessageService.Show(Strings.Status_OpenTerracottaProjectFailed);
    }

    private static string MapCreationFailure(MultiplayerLobbyCreationFailure failure)
    {
        return failure switch
        {
            MultiplayerLobbyCreationFailure.TerracottaUnavailable => Strings.Multiplayer_Create_TerracottaUnavailable,
            MultiplayerLobbyCreationFailure.MinecraftWorldUnavailable => Strings.Multiplayer_Create_WorldUnavailable,
            MultiplayerLobbyCreationFailure.TerracottaBusy => Strings.Multiplayer_Create_TerracottaBusy,
            MultiplayerLobbyCreationFailure.TerracottaProtocolFailed => Strings.Multiplayer_Create_TerracottaProtocolFailed,
            _ => Strings.Multiplayer_Create_LobbyFailed
        };
    }

    private static string MapJoinFailure(MultiplayerLobbyCreationFailure failure)
    {
        return failure switch
        {
            MultiplayerLobbyCreationFailure.InvalidRoomCode => Strings.Multiplayer_Join_InvalidRoomCode,
            MultiplayerLobbyCreationFailure.TerracottaUnavailable => Strings.Multiplayer_Create_TerracottaUnavailable,
            MultiplayerLobbyCreationFailure.TerracottaBusy => Strings.Multiplayer_Create_TerracottaBusy,
            MultiplayerLobbyCreationFailure.TerracottaProtocolFailed => Strings.Multiplayer_Create_TerracottaProtocolFailed,
            _ => Strings.Multiplayer_Join_Failed
        };
    }

    partial void OnCreateLobbyStepChanged(MultiplayerCreateLobbyStep value)
    {
        if (value is not MultiplayerCreateLobbyStep.Lobby)
            IsLeaveLobbyDialogOpen = false;

        OnPropertyChanged(nameof(IsLobbyStep));
        OnPropertyChanged(nameof(IsTerracottaModeSwitchVisible));
        OnPropertyChanged(nameof(SectionTitle));
        RequestLeaveLobbyCommand.NotifyCanExecuteChanged();
        ConfirmLeaveLobbyCommand.NotifyCanExecuteChanged();
    }

    partial void OnTerracottaModeChanged(MultiplayerTerracottaMode value)
    {
        OnPropertyChanged(nameof(IsCreateLobbySection));
        OnPropertyChanged(nameof(IsJoinLobbySection));
        OnPropertyChanged(nameof(IsTerracottaCreateMode));
        OnPropertyChanged(nameof(IsTerracottaJoinMode));
    }

    partial void OnLobbyOwnerNameChanged(string value)
    {
        OnPropertyChanged(nameof(LobbyTitle));
        OnPropertyChanged(nameof(SectionTitle));
    }

    partial void OnRoomCodeChanged(string value)
    {
        CopyRoomCodeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCreatingLobbyChanged(bool value)
    {
        CancelLobbyDetectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsJoiningLobbyChanged(bool value)
    {
        OnPropertyChanged(nameof(JoinLobbyButtonText));
    }

    partial void OnIsLobbyHostChanged(bool value)
    {
        OnPropertyChanged(nameof(LeaveLobbyButtonText));
        OnPropertyChanged(nameof(LeaveLobbyDialogTitle));
        OnPropertyChanged(nameof(LeaveLobbyDialogMessage));
        OnPropertyChanged(nameof(LeaveLobbyConfirmButtonText));
    }

    partial void OnJoinLobbyStatusChanged(string value)
    {
        OnPropertyChanged(nameof(HasJoinLobbyStatus));
    }

    partial void OnSelectedSectionChanged(MultiplayerSectionItem? value)
    {
        foreach (var section in Sections)
            section.IsSelected = ReferenceEquals(section, value);

        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(IsTerracottaSection));
        OnPropertyChanged(nameof(IsTerracottaModeSwitchVisible));
        OnPropertyChanged(nameof(IsCreateLobbySection));
        OnPropertyChanged(nameof(IsJoinLobbySection));
        OnPropertyChanged(nameof(IsRedstoneSection));
    }

}
