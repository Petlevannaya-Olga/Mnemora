using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Mnemora.Desktop.Controls.Loading;

public partial class LoadingIndicator : UserControl
{
    private const int DefaultShowDelay = 1000;
    private const int DefaultMinimumVisibleDuration = 300;

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

    public static readonly DependencyProperty ShowDelayProperty = DependencyProperty.Register(
        nameof(ShowDelay),
        typeof(int),
        typeof(LoadingIndicator),
        new PropertyMetadata(DefaultShowDelay, OnTimingPropertyChanged));

    public static readonly DependencyProperty MinimumVisibleDurationProperty = DependencyProperty.Register(
        nameof(MinimumVisibleDuration),
        typeof(int),
        typeof(LoadingIndicator),
        new PropertyMetadata(DefaultMinimumVisibleDuration, OnTimingPropertyChanged));

    private static readonly DependencyPropertyKey IsPresentedPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsPresented),
        typeof(bool),
        typeof(LoadingIndicator),
        new PropertyMetadata(false));

    public static readonly DependencyProperty IsPresentedProperty = IsPresentedPropertyKey.DependencyProperty;

    private readonly DispatcherTimer _showTimer;
    private readonly DispatcherTimer _hideTimer;
    private DateTime? _shownAt;
    private bool _isIndicatorVisible;
    private int _presentationVersion;

    public LoadingIndicator()
    {
        InitializeComponent();

        _showTimer = new DispatcherTimer();
        _showTimer.Tick += ShowTimer_OnTick;

        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += HideTimer_OnTick;

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

    /// <summary>
    /// Задержка перед фактическим показом индикатора, в миллисекундах.
    /// Короткие операции успевают завершиться до её окончания и не вызывают мерцание UI.
    /// </summary>
    public int ShowDelay
    {
        get => (int)GetValue(ShowDelayProperty);
        set => SetValue(ShowDelayProperty, value);
    }

    /// <summary>
    /// Минимальное время, в течение которого уже показанный индикатор считается активным,
    /// в миллисекундах. Это предотвращает короткое исчезновение/повторное появление при
    /// быстро следующих друг за другом состояниях загрузки.
    /// </summary>
    public int MinimumVisibleDuration
    {
        get => (int)GetValue(MinimumVisibleDurationProperty);
        set => SetValue(MinimumVisibleDurationProperty, value);
    }

    /// <summary>
    /// True only when the indicator has passed ShowDelay and is actually painted.
    /// Loading hosts can bind their own visual chrome to this property so that
    /// a dark background never flashes before the indicator itself appears.
    /// </summary>
    public bool IsPresented => (bool)GetValue(IsPresentedProperty);

    private static void OnVisualPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is LoadingIndicator indicator && indicator.IsLoaded)
        {
            indicator.UpdateVisualState();
        }
    }

    private static void OnTimingPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not LoadingIndicator indicator || !indicator.IsLoaded)
        {
            return;
        }

        indicator.RefreshPresentationState(restartDelay: true);
    }

    private void LoadingIndicator_OnLoaded(object sender, RoutedEventArgs e)
    {
        HideImmediately();
        UpdateVisualState();
        RefreshPresentationState();
    }

    private void LoadingIndicator_OnUnloaded(object sender, RoutedEventArgs e)
    {
        _showTimer.Stop();
        _hideTimer.Stop();
        StopIndeterminateAnimation();
        IndicatorContent.Visibility = Visibility.Collapsed;
        IndicatorContent.Opacity = 0;
        SetValue(IsPresentedPropertyKey, false);
        _isIndicatorVisible = false;
        _shownAt = null;
        _presentationVersion++;
    }

    private void LoadingIndicator_OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded)
        {
            RefreshPresentationState();
        }
    }

    private void Track_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded && _isIndicatorVisible)
        {
            UpdateVisualState();
        }
    }

    private void RefreshPresentationState(bool restartDelay = false)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (IsVisible)
        {
            RequestShow(restartDelay);
        }
        else
        {
            RequestHide();
        }
    }

    private void RequestShow(bool restartDelay)
    {
        _hideTimer.Stop();

        if (_isIndicatorVisible)
        {
            return;
        }

        int delay = Math.Max(0, ShowDelay);

        if (delay == 0)
        {
            _showTimer.Stop();
            ShowImmediately();
            return;
        }

        if (_showTimer.IsEnabled && !restartDelay)
        {
            return;
        }

        _showTimer.Stop();
        _showTimer.Interval = TimeSpan.FromMilliseconds(delay);
        _showTimer.Start();
    }

    private void RequestHide()
    {
        _showTimer.Stop();
        SetValue(IsPresentedPropertyKey, false);

        if (!_isIndicatorVisible)
        {
            IndicatorContent.Opacity = 0;
            IndicatorContent.Visibility = Visibility.Collapsed;
            return;
        }

        int minimumDuration = Math.Max(0, MinimumVisibleDuration);

        if (minimumDuration == 0 || _shownAt is null)
        {
            HideImmediately();
            return;
        }

        TimeSpan elapsed = DateTime.UtcNow - _shownAt.Value;
        TimeSpan remaining = TimeSpan.FromMilliseconds(minimumDuration) - elapsed;

        if (remaining <= TimeSpan.Zero)
        {
            HideImmediately();
            return;
        }

        _hideTimer.Stop();
        _hideTimer.Interval = remaining;
        _hideTimer.Start();
    }

    private void ShowTimer_OnTick(object? sender, EventArgs e)
    {
        _showTimer.Stop();

        if (IsLoaded && IsVisible)
        {
            ShowImmediately();
        }
    }

    private void HideTimer_OnTick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();

        if (!IsVisible)
        {
            HideImmediately();
        }
    }

    private void ShowImmediately()
    {
        int version = ++_presentationVersion;

        // Сначала включаем layout, но не даём контролу отрисоваться.
        // Иначе у indeterminate-полосы на один кадр виден сегмент у левого края,
        // пока Track.ActualWidth ещё не рассчитан.
        IndicatorContent.Opacity = 0;
        IndicatorContent.Visibility = Visibility.Visible;
        _isIndicatorVisible = true;
        _shownAt = DateTime.UtcNow;

        PrepareHiddenIndeterminateState();
        UpdateVisualState();

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (version != _presentationVersion ||
                    !_isIndicatorVisible ||
                    !IsVisible ||
                    !IsLoaded)
                {
                    return;
                }

                UpdateVisualState();
                IndicatorContent.Opacity = 1;
                SetValue(IsPresentedPropertyKey, true);
            }));
    }

    private void HideImmediately()
    {
        _presentationVersion++;
        _showTimer.Stop();
        _hideTimer.Stop();
        IndicatorContent.Opacity = 0;
        IndicatorContent.Visibility = Visibility.Collapsed;
        SetValue(IsPresentedPropertyKey, false);
        _isIndicatorVisible = false;
        _shownAt = null;
        StopIndeterminateAnimation();
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

        if (IsIndeterminate && _isIndicatorVisible)
        {
            StartIndeterminateAnimation();
        }
        else
        {
            StopIndeterminateAnimation();

            if (!IsIndeterminate)
            {
                UpdateDeterminateFill();
            }
        }
    }

    private void PrepareHiddenIndeterminateState()
    {
        if (!IsIndeterminate)
        {
            return;
        }

        double trackWidth = Track.ActualWidth > 0
            ? Track.ActualWidth
            : Math.Max(0d, IndicatorLength);
        double segmentWidth = Math.Clamp(trackWidth * 0.3d, 76d, 140d);

        IndeterminateFill.Width = segmentWidth;
        IndeterminateTranslation.X = -segmentWidth;
    }

    private void StartIndeterminateAnimation()
    {
        if (!_isIndicatorVisible || !IsVisible || Track.ActualWidth <= 0)
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

        if (!_isIndicatorVisible || !IsVisible || Track.ActualWidth <= 0)
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
