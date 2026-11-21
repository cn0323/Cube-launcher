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

namespace Launcher.App.ViewModels.Multiplayer;

/// <summary>
/// 联机页的一级分区：陶瓦联机（Terracotta）与红石联机。
/// 陶瓦联机内部再分为「创建房间 / 加入房间」两个模式，见 <see cref="MultiplayerTerracottaMode"/>。
/// </summary>
public enum MultiplayerPageSection
{
    Terracotta,
    Redstone
}

/// <summary>
/// 陶瓦联机里的两个操作模式。两者共用同一套房间与玩家列表状态。
/// </summary>
public enum MultiplayerTerracottaMode
{
    Create,
    Join
}
