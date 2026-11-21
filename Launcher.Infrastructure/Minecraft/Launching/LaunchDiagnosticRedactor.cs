/*
 * Cube Launcher
 * Copyright (C) 2026 重念
 * SPDX-License-Identifier: GPL-3.0-only
 */

using Launcher.Application.Services;

namespace Launcher.Infrastructure.Minecraft;

internal static class LaunchDiagnosticRedactor
{
    public static string Redact(string text, IReadOnlyList<string> sensitiveValues)
        => SensitiveLogText.Redact(text, sensitiveValues);
}
