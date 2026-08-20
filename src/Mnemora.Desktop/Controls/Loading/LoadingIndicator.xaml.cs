using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mnemora.Desktop.Controls.Loading;

public partial class LoadingIndicator : UserControl
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(LoadingIndicator),
        new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty IndicatorLengthProperty = DependencyProperty.Register(
        nameof(IndicatorLength),
        typeof(double),
        typeof(LoadingIndicator),
        new FrameworkPropertyMetadata(
            360d,
            FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnVisualPropertyChanged));

    public static readonly DependencyProperty IndicatorThicknessProperty = DependencyProperty.Register(
        nameof(IndicatorThickness),
        typeof(double),
        typeof(LoadingIndicator),
        new FrameworkPropertyMetadata(
            8d,
            FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate),
        typeof(bool),
        typeof(LoadingIndicator),
        new PropertyMetadata(true, OnVisualPropertyChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(LoadingIndicator),
        new PropertyMetadata(0d, OnVisualPropertyChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(LoadingIndicator),
        new PropertyMetadata(100d, OnVisualPropertyChanged));

    public static readonly DependencyProperty ShowPercentageProperty = DependencyProperty.Register(
        nameof(ShowPercentage),
        typeof(bool),
        typeof(LoadingIndicator),
        new PropertyMetadata(false, OnVisualPropertyChanged));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(LoadingIndicator),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(220, 229, 237))));

    public static readonly DependencyProperty MessageForegroundProperty = DependencyProperty.Register(
        nameof(MessageForeground),
        typeof(Brush),
        typeof(LoadingIndicator),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(101, 116, 139))));

    public static readonly DependencyProperty PercentageForegroundProperty = DependencyProperty.Register(
        nameof(PercentageForeground),
        typeof(Brush),
        typeof(LoadingIndicator),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(22, 205, 183))));

    public LoadingIndicator()
    {
        InitializeComponent();
        Loaded += LoadingIndicator_OnLoaded;
        Unloaded += LoadingIndicator_OnUnloaded;
        IsVisibleChanged += LoadingIndicator_OnIsVisibleChanged;
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public double IndicatorLength
    {
        get => (double)GetValue(IndicatorLengthProperty);
        set => SetValue(IndicatorLengthProperty, value);
    }

    public double IndicatorThickness
    {
        get => (double)GetValue(IndicatorThicknessProperty);
        set => SetValue(IndicatorThicknessProperty, value);
    }

    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public bool ShowPercentage
    {
        get => (bool)GetValue(ShowPercentageProperty);
        set => SetValue(ShowPercentageProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush MessageForeground
    {
        get => (Brush)GetValue(MessageForegroundProperty);
        set => SetValue(MessageForegroundProperty, value);
    }

    public Brush PercentageForeground
    {
        get => (Brush)GetValue(PercentageForegroundProperty);
        set => SetValue(PercentageForegroundProperty, value);
    }

    private static void OnVisualPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is LoadingIndicator indicator && indicator.IsLoaded)
        {
            indicator.UpdateVisualState();
        }
    }

    private void LoadingIndicator_OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateVisualState();
    }

    private void LoadingIndicator_OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopIndeterminateAnimation();
    }

    private void LoadingIndicator_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateVisualState();
        }
    }

    private void Track_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        bool hasMessage = !string.IsNullOrWhiteSpace(Message);

        MessageTextBlock.Visibility = hasMessage
            ? Visibility.Visible
            : Visibility.Collapsed;
        MessageTextBlock.HorizontalAlignment = ShowPercentage
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Center;

        PercentageTextBlock.Visibility = ShowPercentage
            ? Visibility.Visible
            : Visibility.Collapsed;

        DeterminateFill.Visibility = IsIndeterminate
            ? Visibility.Collapsed
            : Visibility.Visible;
        IndeterminateFill.Visibility = IsIndeterminate
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (IsIndeterminate)
        {
            StartIndeterminateAnimation();
        }
        else
        {
            StopIndeterminateAnimation();
            UpdateDeterminateFill();
        }
    }

    private void StartIndeterminateAnimation()
    {
        if (!IsVisible || Track.ActualWidth <= 0)
        {
            StopIndeterminateAnimation();
            return;
        }

        double segmentWidth = Math.Clamp(Track.ActualWidth * 0.3d, 76d, 140d);
        IndeterminateFill.Width = segmentWidth;

        var animation = new DoubleAnimation
        {
            From = -segmentWidth,
            To = Track.ActualWidth,
            Duration = TimeSpan.FromMilliseconds(1250),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseInOut,
            },
        };

        IndeterminateTranslation.BeginAnimation(
            TranslateTransform.XProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void StopIndeterminateAnimation()
    {
        IndeterminateTranslation.BeginAnimation(
            TranslateTransform.XProperty,
            null);
    }

    private void UpdateDeterminateFill()
    {
        double maximum = Maximum <= 0 ? 100d : Maximum;
        double normalizedValue = Math.Clamp(Value, 0d, maximum) / maximum;
        double targetWidth = Track.ActualWidth * normalizedValue;
        double currentWidth = Math.Max(0d, DeterminateFill.ActualWidth);

        DeterminateFill.Width = targetWidth;

        if (!IsVisible || Track.ActualWidth <= 0)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            From = currentWidth,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut,
            },
        };

        DeterminateFill.BeginAnimation(
            FrameworkElement.WidthProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }
}
