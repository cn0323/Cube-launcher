/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 * SPDX-License-Identifier: GPL-3.0-only
 */

namespace Launcher.App.Services;

public sealed class LiquidGlassChangedEventArgs : EventArgs
{
    public LiquidGlassChangedEventArgs(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    public bool IsEnabled { get; }
}
