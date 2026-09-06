using System.Windows;
using System.Windows.Controls.Primitives;

namespace Mnemora.Desktop.Views.Library;

public static class ResponsiveUniformGridBehavior
{
    public static readonly DependencyProperty DesiredColumnsProperty =
        DependencyProperty.RegisterAttached(
            "DesiredColumns",
            typeof(int),
            typeof(ResponsiveUniformGridBehavior),
            new PropertyMetadata(0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty MinimumItemWidthProperty =
        DependencyProperty.RegisterAttached(
            "MinimumItemWidth",
            typeof(double),
            typeof(ResponsiveUniformGridBehavior),
            new PropertyMetadata(220d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty MaximumColumnsProperty =
        DependencyProperty.RegisterAttached(
            "MaximumColumns",
            typeof(int),
            typeof(ResponsiveUniformGridBehavior),
            new PropertyMetadata(7, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ActualColumnsProperty =
        DependencyProperty.RegisterAttached(
            "ActualColumns",
            typeof(int),
            typeof(ResponsiveUniformGridBehavior),
            new FrameworkPropertyMetadata(
                1,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private static readonly DependencyProperty IsListeningProperty =
        DependencyProperty.RegisterAttached(
            "IsListening",
            typeof(bool),
            typeof(ResponsiveUniformGridBehavior),
            new PropertyMetadata(false));

    public static int GetDesiredColumns(DependencyObject element)
    {
        return (int)element.GetValue(DesiredColumnsProperty);
    }

    public static void SetDesiredColumns(DependencyObject element, int value)
    {
        element.SetValue(DesiredColumnsProperty, value);
    }

    public static double GetMinimumItemWidth(DependencyObject element)
    {
        return (double)element.GetValue(MinimumItemWidthProperty);
    }

    public static void SetMinimumItemWidth(DependencyObject element, double value)
    {
        element.SetValue(MinimumItemWidthProperty, value);
    }

    public static int GetMaximumColumns(DependencyObject element)
    {
        return (int)element.GetValue(MaximumColumnsProperty);
    }

    public static void SetMaximumColumns(DependencyObject element, int value)
    {
        element.SetValue(MaximumColumnsProperty, value);
    }

    public static int GetActualColumns(DependencyObject element)
    {
        return (int)element.GetValue(ActualColumnsProperty);
    }

    public static void SetActualColumns(DependencyObject element, int value)
    {
        element.SetValue(ActualColumnsProperty, value);
    }

    private static void OnLayoutPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not UniformGrid grid)
        {
            return;
        }

        EnsureListening(grid);
        UpdateColumns(grid);
    }

    private static void EnsureListening(UniformGrid grid)
    {
        if ((bool)grid.GetValue(IsListeningProperty))
        {
            return;
        }

        grid.SetValue(IsListeningProperty, true);
        grid.Loaded += Grid_OnLoaded;
        grid.SizeChanged += Grid_OnSizeChanged;
    }

    private static void Grid_OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is UniformGrid grid)
        {
            UpdateColumns(grid);
        }
    }

    private static void Grid_OnSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        if (sender is UniformGrid grid)
        {
            UpdateColumns(grid);
        }
    }

    private static void UpdateColumns(UniformGrid grid)
    {
        double width = grid.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        double minimumItemWidth = Math.Max(
            1d,
            GetMinimumItemWidth(grid));

        int maximumColumns = Math.Max(
            1,
            GetMaximumColumns(grid));

        int availableColumns = Math.Max(
            1,
            (int)Math.Floor(width / minimumItemWidth));

        availableColumns = Math.Min(
            availableColumns,
            maximumColumns);

        int desiredColumns = GetDesiredColumns(grid);
        int columns = desiredColumns <= 0
            ? availableColumns
            : Math.Min(desiredColumns, availableColumns);

        columns = Math.Max(1, columns);

        if (grid.Columns != columns)
        {
            grid.Columns = columns;
        }

        if (GetActualColumns(grid) != columns)
        {
            grid.SetCurrentValue(ActualColumnsProperty, columns);
        }
    }
}
