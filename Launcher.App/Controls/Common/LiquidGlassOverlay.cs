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
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Launcher.App.Controls;

/// <summary>
/// 液态玻璃覆盖层：在窗口背景之上绘制缓慢漂移的柔光与斜向扫光，
/// 让半透明面板看上去像压在一层有流动感的玻璃上。
/// 仅在“液态玻璃效果”开启时可见；不可见时立即停止渲染循环，避免空转耗电。
/// </summary>
public sealed class LiquidGlassOverlay : FrameworkElement
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(40);

    public static readonly DependencyProperty TintBrushProperty =
        DependencyProperty.Register(
            nameof(TintBrush),
            typeof(Brush),
            typeof(LiquidGlassOverlay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowPrimaryBrushProperty =
        DependencyProperty.Register(
            nameof(GlowPrimaryBrush),
            typeof(Brush),
            typeof(LiquidGlassOverlay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowSecondaryBrushProperty =
        DependencyProperty.Register(
            nameof(GlowSecondaryBrush),
            typeof(Brush),
            typeof(LiquidGlassOverlay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpecularBrushProperty =
        DependencyProperty.Register(
            nameof(SpecularBrush),
            typeof(Brush),
            typeof(LiquidGlassOverlay),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowOpacityProperty =
        DependencyProperty.Register(
            nameof(GlowOpacity),
            typeof(double),
            typeof(LiquidGlassOverlay),
            new FrameworkPropertyMetadata(0.9d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SpecularOpacityProperty =
        DependencyProperty.Register(
            nameof(SpecularOpacity),
            typeof(double),
            typeof(LiquidGlassOverlay),
            new FrameworkPropertyMetadata(0.5d, FrameworkPropertyMetadataOptions.AffectsRender));

    // 指针高光：半径克制在窗口短边的一半以内，和苹果液态玻璃的局部反射一致。
    private static readonly Brush PointerHighlightBrush = CreatePointerHighlightBrush();

    private readonly Stopwatch clock = Stopwatch.StartNew();
    private TimeSpan lastFrame;
    private bool isRendering;
    private Point pointerTarget;
    private Point pointerSmooth;
    private bool pointerActive;

    public LiquidGlassOverlay()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SnapsToDevicePixels = false;
        IsVisibleChanged += OnIsVisibleChanged;
        Loaded += (_, _) => UpdateRenderingSubscription();
        Unloaded += (_, _) => StopRendering();
    }

    public Brush? TintBrush
    {
        get => (Brush?)GetValue(TintBrushProperty);
        set => SetValue(TintBrushProperty, value);
    }

    public Brush? GlowPrimaryBrush
    {
        get => (Brush?)GetValue(GlowPrimaryBrushProperty);
        set => SetValue(GlowPrimaryBrushProperty, value);
    }

    public Brush? GlowSecondaryBrush
    {
        get => (Brush?)GetValue(GlowSecondaryBrushProperty);
        set => SetValue(GlowSecondaryBrushProperty, value);
    }

    public Brush? SpecularBrush
    {
        get => (Brush?)GetValue(SpecularBrushProperty);
        set => SetValue(SpecularBrushProperty, value);
    }

    public double GlowOpacity
    {
        get => (double)GetValue(GlowOpacityProperty);
        set => SetValue(GlowOpacityProperty, value);
    }

    public double SpecularOpacity
    {
        get => (double)GetValue(SpecularOpacityProperty);
        set => SetValue(SpecularOpacityProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var bounds = new Rect(RenderSize);
        if (bounds.Width <= 0d || bounds.Height <= 0d)
            return;

        var seconds = clock.Elapsed.TotalSeconds;

        if (TintBrush is { } tintBrush)
            drawingContext.DrawRectangle(tintBrush, null, bounds);

        DrawPointerHighlight(drawingContext, bounds);

        drawingContext.PushOpacity(ClampOpacity(GlowOpacity));
        DrawGlow(
            drawingContext,
            GlowPrimaryBrush,
            bounds,
            centerX: 0.30d + 0.11d * Math.Sin(seconds * 0.21d),
            centerY: 0.32d + 0.09d * Math.Cos(seconds * 0.27d),
            radiusScale: 0.62d);
        DrawGlow(
            drawingContext,
            GlowSecondaryBrush,
            bounds,
            centerX: 0.70d + 0.10d * Math.Cos(seconds * 0.17d),
            centerY: 0.66d + 0.10d * Math.Sin(seconds * 0.23d),
            radiusScale: 0.55d);
        drawingContext.Pop();

        if (SpecularBrush is { } specularBrush)
        {
            // 高光带沿对角线缓慢横扫，形成“玻璃表面在流动”的错觉。
            var sweep = (Math.Sin(seconds * 0.12d) + 1d) / 2d;
            var offsetX = bounds.Width * (sweep * 1.6d - 0.8d);
            var band = new Rect(
                bounds.X + offsetX,
                bounds.Y - bounds.Height * 0.35d,
                bounds.Width * 0.9d,
                bounds.Height * 1.7d);

            drawingContext.PushOpacity(ClampOpacity(SpecularOpacity));
            drawingContext.PushTransform(new RotateTransform(18d, bounds.Width / 2d, bounds.Height / 2d));
            drawingContext.DrawRectangle(specularBrush, null, band);
            drawingContext.Pop();
            drawingContext.Pop();
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }

    private void DrawPointerHighlight(DrawingContext drawingContext, Rect bounds)
    {
        if (!pointerActive)
            return;

        // 缓动跟随，避免鼠标快速移动时高光生硬跳变。
        pointerSmooth = new Point(
            pointerSmooth.X + (pointerTarget.X - pointerSmooth.X) * 0.16d,
            pointerSmooth.Y + (pointerTarget.Y - pointerSmooth.Y) * 0.16d);

        if (pointerSmooth.X < bounds.X - bounds.Width
            || pointerSmooth.X > bounds.Right + bounds.Width
            || pointerSmooth.Y < bounds.Y - bounds.Height
            || pointerSmooth.Y > bounds.Bottom + bounds.Height)
        {
            return;
        }

        var radius = Math.Min(bounds.Width, bounds.Height) * 0.46d;
        drawingContext.DrawEllipse(PointerHighlightBrush, null, pointerSmooth, radius, radius);
    }

    private static Brush CreatePointerHighlightBrush()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5d, 0.5d),
            GradientOrigin = new Point(0.5d, 0.5d),
            RadiusX = 0.5d,
            RadiusY = 0.5d
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(42, 255, 255, 255), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(18, 214, 240, 255), 0.45d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1d));
        brush.Freeze();
        return brush;
    }

    private static void DrawGlow(
        DrawingContext drawingContext,
        Brush? brush,
        Rect bounds,
        double centerX,
        double centerY,
        double radiusScale)
    {
        if (brush is null)
            return;

        var width = bounds.Width * radiusScale;
        var height = bounds.Height * radiusScale;
        var center = new Point(
            bounds.X + (bounds.Width * centerX),
            bounds.Y + (bounds.Height * centerY));
        drawingContext.DrawEllipse(brush, null, center, width / 2d, height / 2d);
    }

    private static double ClampOpacity(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0d, 1d) : 1d;

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        UpdateRenderingSubscription();

    private void UpdateRenderingSubscription()
    {
        if (IsVisible && IsLoaded)
            StartRendering();
        else
            StopRendering();
    }

    private void StartRendering()
    {
        if (isRendering)
            return;

        lastFrame = TimeSpan.Zero;
        isRendering = true;
        CompositionTarget.Rendering += OnCompositionTargetRendering;
    }

    private void StopRendering()
    {
        if (!isRendering)
            return;

        isRendering = false;
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        var now = clock.Elapsed;
        if (now - lastFrame < FrameInterval)
            return;

        lastFrame = now;

        if (IsMouseOver && ActualWidth > 0d && ActualHeight > 0d)
        {
            var position = Mouse.GetPosition(this);
            if (position.X >= 0d && position.Y >= 0d
                && position.X <= ActualWidth && position.Y <= ActualHeight)
            {
                if (!pointerActive)
                    pointerSmooth = position;

                pointerTarget = position;
                pointerActive = true;
            }
            else
            {
                pointerActive = false;
            }
        }
        else
        {
            pointerActive = false;
        }

        InvalidateVisual();
    }
}
