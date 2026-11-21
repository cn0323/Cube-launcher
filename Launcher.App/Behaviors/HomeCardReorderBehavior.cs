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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Launcher.App.Behaviors;

/// <summary>
/// 让面板里的卡片支持「长按拖动换位」。
/// 长按约 0.4 秒后卡片轻微抬起并跟随鼠标，其余卡片平滑让位，松手后落位并把新顺序回调给视图模型。
/// 只在被标记 <see cref="CardKeyProperty"/> 的子元素之间排序，其余内容不受影响。
/// </summary>
public static class HomeCardReorderBehavior
{
    private static readonly TimeSpan LongPressDelay = TimeSpan.FromMilliseconds(420);
    private static readonly TimeSpan LiftDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan ShiftDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan DropDuration = TimeSpan.FromMilliseconds(240);

    // 手指/鼠标轻微抖动不应取消长按，超过该距离才判定为普通拖动/点击。
    private const double MoveTolerance = 8d;
    private const double LiftedScale = 1.045d;
    private const int DraggingZIndex = 100;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(HomeCardReorderBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty OrderProperty = DependencyProperty.RegisterAttached(
        "Order",
        typeof(string),
        typeof(HomeCardReorderBehavior),
        new PropertyMetadata(null, OnOrderChanged));

    public static readonly DependencyProperty OrderChangedCommandProperty = DependencyProperty.RegisterAttached(
        "OrderChangedCommand",
        typeof(ICommand),
        typeof(HomeCardReorderBehavior),
        new PropertyMetadata(null));

    public static readonly DependencyProperty LongPressCommandProperty = DependencyProperty.RegisterAttached(
        "LongPressCommand",
        typeof(ICommand),
        typeof(HomeCardReorderBehavior),
        new PropertyMetadata(null));

    public static readonly DependencyProperty CardKeyProperty = DependencyProperty.RegisterAttached(
        "CardKey",
        typeof(string),
        typeof(HomeCardReorderBehavior),
        new PropertyMetadata(null));

    private static readonly DependencyProperty SessionProperty = DependencyProperty.RegisterAttached(
        "Session",
        typeof(ReorderSession),
        typeof(HomeCardReorderBehavior),
        new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static string? GetOrder(DependencyObject element) => (string?)element.GetValue(OrderProperty);

    public static void SetOrder(DependencyObject element, string? value) => element.SetValue(OrderProperty, value);

    public static ICommand? GetOrderChangedCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(OrderChangedCommandProperty);

    public static void SetOrderChangedCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(OrderChangedCommandProperty, value);

    public static ICommand? GetLongPressCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(LongPressCommandProperty);

    public static void SetLongPressCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(LongPressCommandProperty, value);

    public static string? GetCardKey(DependencyObject element) => (string?)element.GetValue(CardKeyProperty);

    public static void SetCardKey(DependencyObject element, string? value) => element.SetValue(CardKeyProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel)
            return;

        if (e.NewValue is true)
        {
            if (panel.GetValue(SessionProperty) is ReorderSession)
                return;

            var session = new ReorderSession(panel);
            panel.SetValue(SessionProperty, session);
            session.Attach();
            session.ApplyOrder(GetOrder(panel));
            return;
        }

        if (panel.GetValue(SessionProperty) is ReorderSession existing)
        {
            existing.Detach();
            panel.ClearValue(SessionProperty);
        }
    }

    private static void OnOrderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Panel panel && panel.GetValue(SessionProperty) is ReorderSession session)
        {
            try
            {
                session.ApplyOrder(e.NewValue as string);
            }
            catch (InvalidOperationException)
            {
                // 顺序来自设置，面板尚未生成对应卡片时忽略即可。
            }
        }
    }

    private static List<FrameworkElement> GetCards(Panel host)
    {
        var cards = new List<FrameworkElement>(host.Children.Count);
        foreach (UIElement child in host.Children)
        {
            if (child is FrameworkElement element && !string.IsNullOrEmpty(GetCardKey(element)))
                cards.Add(element);
        }

        return cards;
    }

    private static FrameworkElement? FindCard(Panel host, DependencyObject? source)
    {
        var current = source;
        while (current is not null && !ReferenceEquals(current, host))
        {
            if (current is FrameworkElement element
                && !string.IsNullOrEmpty(GetCardKey(element))
                && host.Children.Contains(element))
            {
                return element;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private static Point GetVisualCenter(FrameworkElement element, Visual ancestor)
    {
        // 用中心点而不是左上角：中心缩放不会改变中心坐标，位置计算更稳定。
        var center = new Point(element.ActualWidth / 2d, element.ActualHeight / 2d);
        try
        {
            return element.TransformToAncestor(ancestor).Transform(center);
        }
        catch (InvalidOperationException)
        {
            // 元素在拖拽过程中被移出可视树：返回 NaN，调用方跳过这一帧。
            return new Point(double.NaN, double.NaN);
        }
    }

    private static Point GetLayoutCenter(FrameworkElement element, Visual ancestor)
    {
        var center = GetVisualCenter(element, ancestor);
        if (double.IsNaN(center.X) || double.IsNaN(center.Y))
            return center;

        if (element.RenderTransform is TransformGroup group
            && group.Children.Count > 1
            && group.Children[1] is TranslateTransform translate)
        {
            return new Point(center.X - translate.X, center.Y - translate.Y);
        }

        return center;
    }

    private static void EnsureTransforms(FrameworkElement element, out ScaleTransform scale, out TranslateTransform translate)
    {
        if (element.RenderTransform is TransformGroup existing
            && existing.Children.Count > 1
            && existing.Children[0] is ScaleTransform existingScale
            && existing.Children[1] is TranslateTransform existingTranslate)
        {
            scale = existingScale;
            translate = existingTranslate;
            return;
        }

        scale = new ScaleTransform(1d, 1d);
        translate = new TranslateTransform();
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);
        element.RenderTransform = group;
        element.RenderTransformOrigin = new Point(0.5d, 0.5d);
    }

    private static CubicEase CreateEase(EasingMode mode = EasingMode.EaseOut) => new() { EasingMode = mode };

    private static void AnimateDouble(
        DependencyObject target,
        DependencyProperty property,
        double to,
        TimeSpan duration,
        EasingMode mode = EasingMode.EaseOut)
    {
        var animation = new DoubleAnimation(to, duration)
        {
            EasingFunction = CreateEase(mode)
        };
        ((IAnimatable)target).BeginAnimation(property, animation);
    }

    /// <summary>
    /// 每个承载面板一个会话，保存按下、抬起、拖拽三个阶段的状态。
    /// </summary>
    private sealed class ReorderSession
    {
        private readonly Panel host;
        private readonly DispatcherTimer longPressTimer;

        private FrameworkElement? pressedCard;
        private Point pressOrigin;
        private bool isDragging;

        private FrameworkElement? dragCard;
        private Point pointerOffset;
        private Point dragLayoutCenter;
        private bool orderChanged;

        public ReorderSession(Panel host)
        {
            this.host = host;
            longPressTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = LongPressDelay
            };
            longPressTimer.Tick += OnLongPressTick;
        }

        public void Attach()
        {
            host.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            host.PreviewMouseMove += OnPreviewMouseMove;
            host.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            host.LostMouseCapture += OnLostMouseCapture;
            // 绑定值可能早于 XAML 子元素生成，Loaded 时再按保存的顺序排列一次。
            host.Loaded += OnLoaded;
            host.Unloaded += OnUnloaded;
        }

        public void Detach()
        {
            host.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            host.PreviewMouseMove -= OnPreviewMouseMove;
            host.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            host.LostMouseCapture -= OnLostMouseCapture;
            host.Loaded -= OnLoaded;
            host.Unloaded -= OnUnloaded;
            CancelPress();
            EndDrag(animate: false, notifyOrder: false);
        }

        public void ApplyOrder(string? order)
        {
            if (isDragging || string.IsNullOrWhiteSpace(order))
                return;

            var keys = order.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (keys.Length == 0)
                return;

            var cards = GetCards(host);
            var lookup = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
            foreach (var card in cards)
            {
                var key = GetCardKey(card);
                if (!string.IsNullOrEmpty(key))
                    lookup[key] = card;
            }

            var targetIndex = 0;
            foreach (var key in keys)
            {
                if (!lookup.TryGetValue(key, out var card))
                    continue;

                var currentIndex = host.Children.IndexOf(card);
                if (currentIndex != targetIndex)
                {
                    host.Children.Remove(card);
                    host.Children.Insert(targetIndex, card);
                }

                targetIndex++;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => ApplyOrder(GetOrder(host));

        private void OnUnloaded(object sender, RoutedEventArgs e) => CancelPress();

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CancelPress();

            var card = FindCard(host, e.OriginalSource as DependencyObject);
            if (card is null)
                return;

            pressedCard = card;
            pressOrigin = e.GetPosition(host);
            longPressTimer.Start();
        }

        private void OnLongPressTick(object? sender, EventArgs e)
        {
            longPressTimer.Stop();
            if (pressedCard is null)
                return;

            // 长按期间鼠标可能已经松开或移出窗口，此时进入拖拽会“粘住”卡片。
            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                pressedCard = null;
                return;
            }

            try
            {
                // 长按先通知视图进入卡片编辑态，用户继续移动时仍然按拖拽处理。
                var longPressCommand = GetLongPressCommand(host);
                if (longPressCommand?.CanExecute(null) == true)
                    longPressCommand.Execute(null);

                BeginDrag();
            }
            catch (Exception)
            {
                isDragging = false;
                dragCard = null;
                CancelPress();
            }
        }

        private void BeginDrag()
        {
            if (pressedCard is null)
                return;

            dragCard = pressedCard;
            pressedCard = null;
            isDragging = true;
            orderChanged = false;

            EnsureTransforms(dragCard, out var scale, out var translate);
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            translate.X = 0d;
            translate.Y = 0d;
            scale.ScaleX = 1d;
            scale.ScaleY = 1d;

            dragLayoutCenter = GetLayoutCenter(dragCard, host);
            if (double.IsNaN(dragLayoutCenter.X) || double.IsNaN(dragLayoutCenter.Y))
            {
                // 卡片刚刚被移出可视树，放弃这次拖拽，避免后续坐标换算抛出异常。
                isDragging = false;
                dragCard = null;
                CancelPress();
                return;
            }

            var pointer = Mouse.GetPosition(host);
            pointerOffset = new Point(pointer.X - dragLayoutCenter.X, pointer.Y - dragLayoutCenter.Y);

            host.CaptureMouse();
            Panel.SetZIndex(dragCard, DraggingZIndex);

            AnimateDouble(scale, ScaleTransform.ScaleXProperty, LiftedScale, LiftDuration);
            AnimateDouble(scale, ScaleTransform.ScaleYProperty, LiftedScale, LiftDuration);

            var shadow = new DropShadowEffect
            {
                BlurRadius = 28d,
                ShadowDepth = 7d,
                Direction = 270d,
                Color = Colors.Black,
                Opacity = 0d
            };
            dragCard.Effect = shadow;
            AnimateDouble(shadow, DropShadowEffect.OpacityProperty, 0.36d, LiftDuration);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                OnPreviewMouseMoveCore(e);
            }
            catch (Exception)
            {
                // 拖拽期间元素被卸载会让坐标换算失败，就地收尾，不让异常冒到界面线程。
                EndDrag(animate: false, notifyOrder: true);
            }
        }

        private void OnPreviewMouseMoveCore(MouseEventArgs e)
        {
            if (isDragging)
            {
                UpdateDrag(e);
                e.Handled = true;
                return;
            }

            if (pressedCard is null)
                return;

            var current = e.GetPosition(host);
            var delta = current - pressOrigin;
            if (Math.Abs(delta.X) > MoveTolerance || Math.Abs(delta.Y) > MoveTolerance)
                CancelPress();
        }

        private void UpdateDrag(MouseEventArgs e)
        {
            if (dragCard is null)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndDrag(animate: true, notifyOrder: true);
                return;
            }

            EnsureTransforms(dragCard, out _, out var translate);
            var pointer = e.GetPosition(host);
            translate.X = pointer.X - pointerOffset.X - dragLayoutCenter.X;
            translate.Y = pointer.Y - pointerOffset.Y - dragLayoutCenter.Y;

            TryReorder(new Point(pointer.X - pointerOffset.X, pointer.Y - pointerOffset.Y));
        }

        private void TryReorder(Point dragCenter)
        {
            if (dragCard is null)
                return;

            var cards = GetCards(host);
            var dragIndex = cards.IndexOf(dragCard);
            if (dragIndex < 0)
                return;

            for (var index = 0; index < cards.Count; index++)
            {
                if (index == dragIndex)
                    continue;

                var otherCenter = GetVisualCenter(cards[index], host);
                if (double.IsNaN(otherCenter.X) || double.IsNaN(otherCenter.Y))
                    continue;

                var shouldSwap = index < dragIndex
                    ? dragCenter.Y < otherCenter.Y
                    : dragCenter.Y > otherCenter.Y;

                if (!shouldSwap)
                    continue;

                MoveCard(dragIndex, index);
                return;
            }
        }

        private void MoveCard(int fromIndex, int toIndex)
        {
            if (dragCard is null)
                return;

            var cards = GetCards(host);
            if (fromIndex < 0 || fromIndex >= cards.Count || toIndex < 0 || toIndex >= cards.Count)
                return;

            var oldCenters = new Dictionary<FrameworkElement, Point>(cards.Count);
            foreach (var card in cards)
                oldCenters[card] = GetLayoutCenter(card, host);

            host.Children.Remove(dragCard);
            var remaining = GetCards(host);
            var insertAt = toIndex >= remaining.Count
                ? host.Children.Count
                : host.Children.IndexOf(remaining[toIndex]);
            host.Children.Insert(insertAt, dragCard);
            host.UpdateLayout();

            foreach (var card in cards)
            {
                if (ReferenceEquals(card, dragCard))
                    continue;

                var newCenter = GetLayoutCenter(card, host);
                var offset = oldCenters[card] - newCenter;
                if (Math.Abs(offset.X) < 0.1 && Math.Abs(offset.Y) < 0.1)
                    continue;

                EnsureTransforms(card, out _, out var translate);
                translate.BeginAnimation(TranslateTransform.XProperty, null);
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.X = offset.X;
                translate.Y = offset.Y;
                AnimateDouble(translate, TranslateTransform.XProperty, 0d, ShiftDuration);
                AnimateDouble(translate, TranslateTransform.YProperty, 0d, ShiftDuration);
            }

            // 让位动画会改变布局位置，补偿拖拽卡片自身的位移，避免它随重排跳动。
            var newDragCenter = GetLayoutCenter(dragCard, host);
            var dragOffset = oldCenters[dragCard] - newDragCenter;
            EnsureTransforms(dragCard, out _, out var dragTranslate);
            dragTranslate.X += dragOffset.X;
            dragTranslate.Y += dragOffset.Y;
            dragLayoutCenter = newDragCenter;
            orderChanged = true;
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                OnPreviewMouseLeftButtonUpCore(e);
            }
            catch (Exception)
            {
                EndDrag(animate: false, notifyOrder: true);
            }
        }

        private void OnPreviewMouseLeftButtonUpCore(MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                // 拖拽结束后松手不应触发卡片内的按钮点击。
                e.Handled = true;
                EndDrag(animate: true, notifyOrder: true);
                return;
            }

            CancelPress();
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (isDragging)
                EndDrag(animate: true, notifyOrder: true);
        }

        private void CancelPress()
        {
            longPressTimer.Stop();
            pressedCard = null;
        }

        private void EndDrag(bool animate, bool notifyOrder)
        {
            if (!isDragging)
                return;

            isDragging = false;
            longPressTimer.Stop();
            var card = dragCard;
            dragCard = null;

            if (host.IsMouseCaptured)
                host.ReleaseMouseCapture();

            if (card is not null)
                ReleaseCard(card, animate);

            if (notifyOrder && orderChanged)
                NotifyOrderChanged();

            orderChanged = false;
        }

        private static void ReleaseCard(FrameworkElement card, bool animate)
        {
            if (card.RenderTransform is not TransformGroup group
                || group.Children.Count < 2
                || group.Children[0] is not ScaleTransform scale
                || group.Children[1] is not TranslateTransform translate)
            {
                ClearCardState(card);
                return;
            }

            if (!animate)
            {
                translate.BeginAnimation(TranslateTransform.XProperty, null);
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                ClearCardState(card);
                return;
            }

            var drop = new DoubleAnimation(0d, DropDuration)
            {
                EasingFunction = CreateEase()
            };
            drop.Completed += (_, _) =>
            {
                try
                {
                    translate.BeginAnimation(TranslateTransform.XProperty, null);
                    translate.BeginAnimation(TranslateTransform.YProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    ClearCardState(card);
                }
                catch (InvalidOperationException)
                {
                    // 动画结束时卡片可能已被卸载，清理失败无需处理。
                }
            };

            translate.BeginAnimation(TranslateTransform.XProperty, drop);
            AnimateDouble(translate, TranslateTransform.YProperty, 0d, DropDuration);
            AnimateDouble(scale, ScaleTransform.ScaleXProperty, 1d, DropDuration);
            AnimateDouble(scale, ScaleTransform.ScaleYProperty, 1d, DropDuration);

            if (card.Effect is DropShadowEffect shadow)
                AnimateDouble(shadow, DropShadowEffect.OpacityProperty, 0d, DropDuration);
        }

        private static void ClearCardState(FrameworkElement card)
        {
            card.ClearValue(UIElement.EffectProperty);
            // 清除本地值即可退回 XAML/样式里原本的变换，不要写成 Transform.Identity：
            // 后者会覆盖模板自带的 RenderTransform，下一次动画就会因为缺少 TransformGroup 而失败。
            card.ClearValue(UIElement.RenderTransformProperty);
            card.RenderTransformOrigin = new Point(0.5d, 0.5d);
            Panel.SetZIndex(card, 0);
        }

        private void NotifyOrderChanged()
        {
            var keys = new List<string>();
            foreach (UIElement child in host.Children)
            {
                if (child is FrameworkElement element)
                {
                    var key = GetCardKey(element);
                    if (!string.IsNullOrEmpty(key))
                        keys.Add(key);
                }
            }

            if (keys.Count == 0)
                return;

            var order = string.Join(',', keys);
            var command = GetOrderChangedCommand(host);
            if (command?.CanExecute(order) == true)
                command.Execute(order);
        }
    }
}
