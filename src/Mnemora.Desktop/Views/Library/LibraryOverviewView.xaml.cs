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
            ? Math.Max(1, viewModel.ActualTilesPerRow)
            : viewModel.IsCompactTilesView
                ? Math.Max(1, viewModel.ActualCompactTilesPerRow)
                : 1;

        viewModel.UpdateViewport(
            GetLogicalEntityOffset(sender, e.VerticalOffset, itemsPerRow));

        if (_isScrollPageLoadRunning)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);
        if (scrollViewer is null ||
            (!IsNearTop(scrollViewer) && !IsNearBottom(scrollViewer)))
        {
            return;
        }

        _isScrollPageLoadRunning = true;
        bool loadPreviousPage =
            IsNearTop(scrollViewer) &&
            viewModel.SectionsHasPrevious;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearTop(scrollViewer) &&
                   viewModel.SectionsHasPrevious &&
                   viewModel.LoadPreviousPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.SectionWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.SectionWindowEndOffset;
                Guid? anchorId = viewModel.Sections.FirstOrDefault()?.Id;

                await viewModel.LoadPreviousPageCommand.ExecuteAsync(null);
                await WaitForScrollLayoutAsync();

                if (anchorId is Guid id)
                {
                    ScrollSectionAnchorIntoView(sender, viewModel, id);
                    await WaitForScrollLayoutAsync();
                }

                viewModel.UpdateViewport(
                    GetLogicalEntityOffset(
                        sender,
                        scrollViewer.VerticalOffset,
                        itemsPerRow));

                if (startOffsetBeforeLoading == viewModel.SectionWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.SectionWindowEndOffset)
                {
                    break;
                }
            }

            if (loadPreviousPage)
            {
                return;
            }

            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearBottom(scrollViewer) &&
                   viewModel.LoadNextPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.SectionWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.SectionWindowEndOffset;
                Guid? anchorId = viewModel.Sections.LastOrDefault()?.Id;

                await viewModel.LoadNextPageCommand.ExecuteAsync(null);
                await WaitForScrollLayoutAsync();

                if (anchorId is Guid id)
                {
                    ScrollSectionAnchorIntoView(sender, viewModel, id);
                    await WaitForScrollLayoutAsync();
                }

                viewModel.UpdateViewport(
                    GetLogicalEntityOffset(
                        sender,
                        scrollViewer.VerticalOffset,
                        itemsPerRow));

                if (startOffsetBeforeLoading == viewModel.SectionWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.SectionWindowEndOffset)
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

    private static bool IsNearTop(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ExtentHeight <= 0 || scrollViewer.ViewportHeight <= 0)
        {
            return false;
        }

        double threshold = Math.Max(2, scrollViewer.ViewportHeight * 0.5);
        return scrollViewer.VerticalOffset <= threshold;
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

    private async Task WaitForScrollLayoutAsync()
    {
        await Dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.Background);
    }

    private static void ScrollSectionAnchorIntoView(
        object sender,
        LibraryOverviewViewModel viewModel,
        Guid anchorId)
    {
        LibrarySectionCardViewModel? anchor =
            viewModel.Sections.FirstOrDefault(section => section.Id == anchorId);

        if (anchor is null)
        {
            return;
        }

        if (sender is DataGrid dataGrid)
        {
            dataGrid.ScrollIntoView(anchor);
            return;
        }

        if (sender is ListBox listBox)
        {
            IEnumerable<LibrarySectionRowViewModel> rows = viewModel.IsTilesView
                ? viewModel.SectionRows
                : viewModel.CompactSectionRows;

            LibrarySectionRowViewModel? row = rows.FirstOrDefault(
                candidate => candidate.Sections.Any(section => section.Id == anchorId));

            if (row is not null)
            {
                listBox.ScrollIntoView(row);
            }

            return;
        }

        if (sender is DependencyObject dependencyObject)
        {
            ListBox? parentListBox = FindVisualParent<ListBox>(dependencyObject);
            if (parentListBox is not null)
            {
                IEnumerable<LibrarySectionRowViewModel> rows = viewModel.IsTilesView
                    ? viewModel.SectionRows
                    : viewModel.CompactSectionRows;

                LibrarySectionRowViewModel? row = rows.FirstOrDefault(
                    candidate => candidate.Sections.Any(section => section.Id == anchorId));

                if (row is not null)
                {
                    parentListBox.ScrollIntoView(row);
                }
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        DependencyObject? current = child;

        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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
