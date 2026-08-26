using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "CancellationTokenSource освобождается при выгрузке представления.")]
public partial class LibraryContainerView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;
    private LibraryContainerViewModel? _loadedViewModel;
    private bool _isFoldersPageLoadRunning;
    private bool _isMaterialsPageLoadRunning;

    public LibraryContainerView()
    {
        InitializeComponent();
    }

    private async void LibraryContainerView_OnLoaded(object sender, RoutedEventArgs e)
    {
        await StartLoadIfReadyAsync();
    }

    private async void LibraryContainerView_OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded)
            await StartLoadIfReadyAsync();
    }

    private void LibraryContainerView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loadedViewModel = null;
        CancelLoading();
    }

    private async Task StartLoadIfReadyAsync()
    {
        if (DataContext is not LibraryContainerViewModel viewModel ||
            ReferenceEquals(_loadedViewModel, viewModel) && _loadCancellationTokenSource is not null)
        {
            return;
        }

        CancelLoading();
        _loadedViewModel = viewModel;

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            await viewModel.LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Уход со страницы или смена DataContext отменяет загрузку.
        }
    }

    private async void FoldersScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isFoldersPageLoadRunning || e.VerticalChange <= 0 ||
            DataContext is not LibraryContainerViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);
        if (scrollViewer is null || !IsNearBottom(scrollViewer) ||
            !viewModel.LoadNextFoldersPageCommand.CanExecute(null))
        {
            return;
        }

        _isFoldersPageLoadRunning = true;

        try
        {
            await viewModel.LoadNextFoldersPageCommand.ExecuteAsync(null);
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

    private async void MaterialsScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isMaterialsPageLoadRunning || e.VerticalChange <= 0 ||
            DataContext is not LibraryContainerViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);
        if (scrollViewer is null || !IsNearBottom(scrollViewer) ||
            !viewModel.LoadNextMaterialsPageCommand.CanExecute(null))
        {
            return;
        }

        _isMaterialsPageLoadRunning = true;

        try
        {
            await viewModel.LoadNextMaterialsPageCommand.ExecuteAsync(null);
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
