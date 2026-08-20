using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "CancellationTokenSource освобождается при выгрузке представления.")]
public partial class LibraryTopicView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;
    private bool _isScrollPageLoadRunning;

    public LibraryTopicView()
    {
        InitializeComponent();
    }

    private async void LibraryTopicView_OnLoaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            if (DataContext is LibraryTopicViewModel viewModel)
            {
                await viewModel.LoadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Закрытие или смена представления прерывает его запросы.
        }
    }

    private void LibraryTopicView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();
    }

    private async void MaterialsScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isScrollPageLoadRunning ||
            DataContext is not LibraryTopicViewModel viewModel)
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
                int materialsCountBeforeLoading = viewModel.Materials.Count;
                await viewModel.LoadNextPageCommand.ExecuteAsync(null);

                // Даём DataGrid пересчитать диапазон прокрутки, а затем повторно
                // проверяем низ. Так событие, возникшее во время загрузки, не теряется.
                await Dispatcher.InvokeAsync(
                    static () => { },
                    DispatcherPriority.Background);

                if (viewModel.Materials.Count == materialsCountBeforeLoading)
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
}
