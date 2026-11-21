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

using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Launcher.App.Services.Networking;

/// <summary>
/// 一次采样得到的网络吞吐（字节/秒）。
/// </summary>
public readonly record struct NetworkThroughput(double ReceivedBytesPerSecond, double SentBytesPerSecond)
{
    public static NetworkThroughput Zero => new(0d, 0d);

    public bool IsZero => ReceivedBytesPerSecond <= 0.5d && SentBytesPerSecond <= 0.5d;
}

/// <summary>
/// 按网卡累计计数器做差，得到整机实时上下行速率。
///
/// 说明：Minecraft 进程本身的流量在 .NET 里没有可直接读取的公开接口，
/// 这里统计的是所有处于 Up 状态、非回环网卡的收发总量 —— 单人游戏时基本为 0，
/// 联机时会跟随游戏流量变化，和 PCL2 展示的口径一致。
/// </summary>
public sealed class NetworkThroughputSampler
{
    private long lastReceived;
    private long lastSent;
    private long lastTimestamp;
    private bool primed;

    /// <summary>重置基线，避免把启动前的累计量算进第一秒。</summary>
    public void Reset()
    {
        primed = false;
        lastReceived = 0;
        lastSent = 0;
        lastTimestamp = 0;
    }

    public NetworkThroughput Sample()
    {
        var (received, sent) = ReadTotals();
        var timestamp = Stopwatch.GetTimestamp();

        if (!primed)
        {
            primed = true;
            lastReceived = received;
            lastSent = sent;
            lastTimestamp = timestamp;
            return NetworkThroughput.Zero;
        }

        var elapsed = (timestamp - lastTimestamp) / (double)Stopwatch.Frequency;
        if (elapsed <= 0.05d)
            return NetworkThroughput.Zero;

        // 网卡计数器是 32 位回绕的，差值为负说明刚回绕，按 0 处理避免出现巨大尖峰。
        var receivedDelta = received >= lastReceived ? received - lastReceived : 0;
        var sentDelta = sent >= lastSent ? sent - lastSent : 0;

        lastReceived = received;
        lastSent = sent;
        lastTimestamp = timestamp;

        return new NetworkThroughput(receivedDelta / elapsed, sentDelta / elapsed);
    }

    private static (long Received, long Sent) ReadTotals()
    {
        long received = 0;
        long sent = 0;

        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return (0, 0);
        }

        foreach (var networkInterface in interfaces)
        {
            try
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up
                    || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var statistics = networkInterface.GetIPv4Statistics();
                received += statistics.BytesReceived;
                sent += statistics.BytesSent;
            }
            catch (NetworkInformationException)
            {
                // 网卡在枚举与读取之间被禁用或拔出，跳过即可。
            }
            catch (PlatformNotSupportedException)
            {
                // 个别虚拟网卡不提供统计，忽略。
            }
        }

        return (received, sent);
    }
}
