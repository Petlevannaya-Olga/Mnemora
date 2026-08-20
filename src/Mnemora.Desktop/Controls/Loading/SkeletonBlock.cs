using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mnemora.Desktop.Controls.Loading;

/// <summary>
/// Лёгкий анимированный заполнитель для состояний загрузки.
/// Анимация работает только пока элемент действительно видим.
/// </summary>
public sealed class SkeletonBlock : Border
{
    private readonly TranslateTransform _shimmerTransform;
    private readonly DoubleAnimation _shimmerAnimation;

    public SkeletonBlock()
    {
        CornerRadius = new CornerRadius(6);
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;

        _shimmerTransform = new TranslateTransform(-1.25, 0);

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            RelativeTransform = _shimmerTransform
        };

        brush.GradientStops.Add(new GradientStop(
            Color.FromRgb(229, 234, 241),
            0));
        brush.GradientStops.Add(new GradientStop(
            Color.FromRgb(248, 250, 252),
            0.5));
        brush.GradientStops.Add(new GradientStop(
            Color.FromRgb(229, 234, 241),
            1));

        Background = brush;

        _shimmerAnimation = new DoubleAnimation
        {
            From = -1.25,
            To = 1.25,
            Duration = TimeSpan.FromMilliseconds(1150),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Loaded += SkeletonBlock_OnLoaded;
        Unloaded += SkeletonBlock_OnUnloaded;
        IsVisibleChanged += SkeletonBlock_OnIsVisibleChanged;
    }

    private void SkeletonBlock_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsVisible)
        {
            StartAnimation();
        }
    }

    private void SkeletonBlock_OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void SkeletonBlock_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (e.NewValue is true)
        {
            StartAnimation();
        }
        else
        {
            StopAnimation();
        }
    }

    private void StartAnimation()
    {
        _shimmerTransform.BeginAnimation(
            TranslateTransform.XProperty,
            _shimmerAnimation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void StopAnimation()
    {
        _shimmerTransform.BeginAnimation(
            TranslateTransform.XProperty,
            null);
    }
}
