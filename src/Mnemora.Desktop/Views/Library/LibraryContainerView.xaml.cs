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
public partial class LibraryContainerView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;
    private bool _isFoldersPageLoadRunning;
    private bool _isMaterialsPageLoadRunning;

    public LibraryContainerView()
    {
        InitializeComponent();
    }

    private async void LibraryContainerView_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        CancelLoading();

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            if (DataContext is LibraryContainerViewModel viewModel)
            {
                await viewModel.LoadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Уход со страницы отменяет её запросы.
        }
    }

    private void LibraryContainerView_OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        CancelLoading();
    }

    private async void FoldersScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (_isFoldersPageLoadRunning ||
            DataContext is not LibraryContainerViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);

        if (scrollViewer is null || !IsNearBottom(scrollViewer))
        {
            return;
        }

        _isFoldersPageLoadRunning = true;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearBottom(scrollViewer) &&
                   viewModel.LoadNextFoldersPageCommand.CanExecute(null))
            {
                int countBefore = viewModel.Folders.Count;
                await viewModel.LoadNextFoldersPageCommand.ExecuteAsync(null);

                await Dispatcher.InvokeAsync(
                    static () => { },
                    DispatcherPriority.Background);

                if (viewModel.Folders.Count == countBefore)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Обычная отмена при навигации или смене фильтра.
        }
        finally
        {
            _isFoldersPageLoadRunning = false;
        }
    }

    private async void MaterialsScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (_isMaterialsPageLoadRunning ||
            DataContext is not LibraryContainerViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);

        if (scrollViewer is null || !IsNearBottom(scrollViewer))
        {
            return;
        }

        _isMaterialsPageLoadRunning = true;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearBottom(scrollViewer) &&
                   viewModel.LoadNextMaterialsPageCommand.CanExecute(null))
            {
                int countBefore = viewModel.Materials.Count;
                await viewModel.LoadNextMaterialsPageCommand.ExecuteAsync(null);

                await Dispatcher.InvokeAsync(
                    static () => { },
                    DispatcherPriority.Background);

                if (viewModel.Materials.Count == countBefore)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Обычная отмена при навигации или смене фильтра.
        }
        finally
        {
            _isMaterialsPageLoadRunning = false;
        }
    }

    private void FolderTableRow_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow
            {
                DataContext: LibraryFolderCardViewModel folder,
            } ||
            DataContext is not LibraryContainerViewModel viewModel ||
            !viewModel.OpenFolderCommand.CanExecute(folder))
        {
            return;
        }

        viewModel.OpenFolderCommand.Execute(folder);
        e.Handled = true;
    }

    private static bool IsNearBottom(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ExtentHeight <= 0 ||
            scrollViewer.ViewportHeight <= 0)
        {
            return false;
        }

        double remainingDistance = Math.Max(
            0,
            scrollViewer.ExtentHeight -
            scrollViewer.VerticalOffset -
            scrollViewer.ViewportHeight);

        double threshold = Math.Max(
            2,
            scrollViewer.ViewportHeight * 0.45);

        return remainingDistance <= threshold;
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
            DependencyObject child =
                VisualTreeHelper.GetChild(parent, index);

            if (child is T result)
            {
                return result;
            }

            T? nested = FindVisualChild<T>(child);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void CancelLoading()
    {
        CancellationTokenSource? cancellationTokenSource =
            _loadCancellationTokenSource;

        _loadCancellationTokenSource = null;

        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }
}
