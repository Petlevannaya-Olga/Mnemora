using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private LibraryContainerViewModel? _loadedViewModel;
    private bool _isFoldersPageLoadRunning;
    private bool _isMaterialsPageLoadRunning;
    private bool _isMixedPageLoadRunning;

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
            ApplyFoldersMaterialsSplit(viewModel);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Уход со страницы или смена DataContext отменяет загрузку.
        }
    }


    private async void FoldersScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isFoldersPageLoadRunning ||
            e.VerticalChange <= 0 ||
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
                int foldersCountBeforeLoading = viewModel.Folders.Count;
                await viewModel.LoadNextFoldersPageCommand.ExecuteAsync(null);

                // Коллекция уже обновлена, но ScrollViewer пересчитывает ExtentHeight
                // на следующем проходе привязки и разметки. После него повторно
                // проверяем низ, чтобы не потерять ScrollChanged во время загрузки.
                await Dispatcher.InvokeAsync(
                    static () => { },
                    DispatcherPriority.Background);

                if (viewModel.Folders.Count == foldersCountBeforeLoading)
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

    private async void MaterialsScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isMaterialsPageLoadRunning ||
            e.VerticalChange <= 0 ||
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
                int materialsCountBeforeLoading = viewModel.Materials.Count;
                await viewModel.LoadNextMaterialsPageCommand.ExecuteAsync(null);

                // После добавления строк DataGrid обновляет диапазон прокрутки
                // асинхронно. Ждём layout и ещё раз проверяем нижнюю границу,
                // чтобы продолжить загрузку без движения вверх-вниз.
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
            // Обычная отмена при навигации или смене фильтра.
        }
        finally
        {
            _isMaterialsPageLoadRunning = false;
        }
    }

    private async void MixedContentScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isMixedPageLoadRunning ||
            e.VerticalChange <= 0 ||
            DataContext is not LibraryContainerViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);
        if (scrollViewer is null || !IsNearBottom(scrollViewer))
        {
            return;
        }

        _isMixedPageLoadRunning = true;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearBottom(scrollViewer) &&
                   viewModel.LoadNextMixedPageCommand.CanExecute(null))
            {
                int countBeforeLoading = viewModel.MixedContent.Count;
                await viewModel.LoadNextMixedPageCommand.ExecuteAsync(null);

                await Dispatcher.InvokeAsync(
                    static () => { },
                    DispatcherPriority.Background);

                if (viewModel.MixedContent.Count == countBeforeLoading)
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
            _isMixedPageLoadRunning = false;
        }
    }

    private async void FoldersMaterialsSplitter_OnDragCompleted(
        object sender,
        DragCompletedEventArgs e)
    {
        if (DataContext is not LibraryContainerViewModel viewModel ||
            !viewModel.HasFolderContent ||
            !viewModel.HasMaterialContent)
        {
            return;
        }

        double panesHeight =
            FoldersPaneRow.ActualHeight +
            MaterialsPaneRow.ActualHeight;

        if (panesHeight <= 0)
        {
            return;
        }

        double foldersPaneRatio =
            FoldersPaneRow.ActualHeight /
            panesHeight;

        await viewModel.SaveFoldersPaneRatioAsync(
            foldersPaneRatio);
    }

    private void ApplyFoldersMaterialsSplit(
        LibraryContainerViewModel viewModel)
    {
        if (!viewModel.HasFolderContent ||
            !viewModel.HasMaterialContent)
        {
            return;
        }

        double foldersPaneRatio =
            Math.Clamp(
                viewModel.FoldersPaneRatio,
                0.1,
                0.9);

        FoldersPaneRow.Height =
            new GridLength(
                foldersPaneRatio,
                GridUnitType.Star);

        MaterialsPaneRow.Height =
            new GridLength(
                1d - foldersPaneRatio,
                GridUnitType.Star);
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

    private void MixedContentTableRow_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow
            {
                DataContext: LibraryContentListItemViewModel
                {
                    IsFolder: true,
                    Folder: { } folder,
                },
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
