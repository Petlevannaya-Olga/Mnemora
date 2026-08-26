using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "CancellationTokenSource is disposed when the WPF view is unloaded.")]
public partial class LibraryOverviewView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;
    private bool _isScrollPageLoadRunning;

    public LibraryOverviewView()
    {
        InitializeComponent();
    }

    private async void LibraryOverviewView_OnLoaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            if (DataContext is LibraryOverviewViewModel viewModel)
            {
                await viewModel.LoadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ignore
        }
    }

    private void LibraryOverviewView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();
    }

    private async void SectionsScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0 ||
            e.ViewportHeight <= 0 ||
            DataContext is not LibraryOverviewViewModel viewModel)
        {
            return;
        }

        int itemsPerRow = viewModel.IsTilesView
            ? viewModel.ActualTilesPerRow
            : viewModel.IsCompactTilesView
                ? viewModel.ActualCompactTilesPerRow
                : 1;

        viewModel.UpdateViewport(
            GetLogicalEntityOffset(sender, e.VerticalOffset, itemsPerRow));

        if (_isScrollPageLoadRunning)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);

        if (scrollViewer is null || !IsNearBottom(scrollViewer))
        {
            return;
        }

        _isScrollPageLoadRunning = true;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearBottom(scrollViewer) &&
                   viewModel.LoadNextPageCommand.CanExecute(null))
            {
                int sectionsCountBeforeLoading = viewModel.Sections.Count;
                await viewModel.LoadNextPageCommand.ExecuteAsync(null);

                // Ждём перерасчёта диапазона прокрутки после добавления разделов.
                // Затем повторно проверяем низ, чтобы не потерять ScrollChanged,
                // пришедший во время выполнения команды.
                await Dispatcher.InvokeAsync(
                    static () => { },
                    DispatcherPriority.Background);

                if (viewModel.Sections.Count == sectionsCountBeforeLoading)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Обычная отмена при уходе со страницы или перезапуске фильтра.
        }
        finally
        {
            _isScrollPageLoadRunning = false;
        }
    }

    private static bool IsNearBottom(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ExtentHeight <= 0 || scrollViewer.ViewportHeight <= 0)
        {
            return false;
        }

        double remainingDistance = Math.Max(
            0,
            scrollViewer.ExtentHeight -
            scrollViewer.VerticalOffset -
            scrollViewer.ViewportHeight);

        double loadingThreshold = Math.Max(2, scrollViewer.ViewportHeight * 0.5);
        return remainingDistance <= loadingThreshold;
    }

    private static ScrollViewer? ResolveScrollViewer(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (e.OriginalSource is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        return sender is DependencyObject dependencyObject
            ? FindVisualChild<ScrollViewer>(dependencyObject)
            : null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

        for (int index = 0; index < childrenCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);

            if (child is T result)
            {
                return result;
            }

            T? nestedResult = FindVisualChild<T>(child);

            if (nestedResult is not null)
            {
                return nestedResult;
            }
        }

        return null;
    }

    private static double GetLogicalEntityOffset(
        object sender,
        double verticalOffset,
        int itemsPerRow)
    {
        if (sender is DataGrid)
        {
            return verticalOffset;
        }

        int rowIndex = Math.Max(0, (int)Math.Floor(verticalOffset));
        return rowIndex * Math.Max(1, itemsPerRow);
    }

    private void CancelLoading()
    {
        var cancellationTokenSource = _loadCancellationTokenSource;
        _loadCancellationTokenSource = null;

        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private void SectionTableRow_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow { DataContext: not null } row ||
            DataContext is not LibraryOverviewViewModel viewModel)
        {
            return;
        }

        if (!viewModel.OpenSectionCommand.CanExecute(row.DataContext))
        {
            return;
        }

        viewModel.OpenSectionCommand.Execute(row.DataContext);
        e.Handled = true;
    }
    
    private void SectionsTable_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not DataGrid dataGrid ||
            e.NewSize.Width <= 0 ||
            e.NewSize.Height <= 0)
        {
            return;
        }

        dataGrid.Clip = new RectangleGeometry(
            new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
            13,
            13);
    }
}
