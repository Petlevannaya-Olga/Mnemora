using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "CancellationTokenSource освобождается при выгрузке представления.")]
public partial class LibrarySectionView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;
    private bool _isScrollPageLoadRunning;

    public LibrarySectionView()
    {
        InitializeComponent();
    }

    private async void LibrarySectionView_OnLoaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            if (DataContext is LibrarySectionViewModel viewModel)
            {
                await viewModel.LoadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Закрытие или смена представления прерывает его запросы.
        }
    }

    private void LibrarySectionView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();
    }

    private async void TopicsScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isScrollPageLoadRunning ||
            DataContext is not LibrarySectionViewModel viewModel)
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
                int topicsCountBeforeLoading = viewModel.Topics.Count;
                await viewModel.LoadNextPageCommand.ExecuteAsync(null);

                // Коллекция уже обновлена, но ScrollViewer пересчитывает ExtentHeight
                // на следующем проходе привязки и разметки. После него ещё раз
                // проверяем низ списка, чтобы не потерять событие ScrollChanged,
                // пришедшее во время выполнения команды.
                await Dispatcher.InvokeAsync(
                    static () => { },
                    DispatcherPriority.Background);

                if (viewModel.Topics.Count == topicsCountBeforeLoading)
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

    private void TopicTableRow_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow { DataContext: LibraryTopicCardViewModel topic } ||
            DataContext is not LibrarySectionViewModel viewModel ||
            !viewModel.OpenTopicCommand.CanExecute(topic))
        {
            return;
        }

        viewModel.OpenTopicCommand.Execute(topic);
        e.Handled = true;
    }

    private void TopicsTable_OnSizeChanged(object sender, SizeChangedEventArgs e)
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
