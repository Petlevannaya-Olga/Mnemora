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



    private void ContentTable_OnSizeChanged(object sender, SizeChangedEventArgs e)
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

    private async void FoldersScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not LibraryContainerViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);
        if (scrollViewer is null)
        {
            return;
        }

        int itemsPerRow = viewModel.IsTilesView
            ? Math.Max(1, viewModel.ActualFolderTilesPerRow)
            : viewModel.IsCompactTilesView
                ? Math.Max(1, viewModel.ActualFolderCompactTilesPerRow)
                : 1;

        viewModel.UpdateFoldersViewport(
            GetLogicalEntityOffset(
                sender,
                scrollViewer,
                viewModel.Folders.Count,
                itemsPerRow));

        if (_isFoldersPageLoadRunning ||
            (!IsNearTop(scrollViewer) && !IsNearBottom(scrollViewer)))
        {
            return;
        }

        _isFoldersPageLoadRunning = true;
        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;

        bool loadPreviousPage =
            IsNearTop(scrollViewer) &&
            viewModel.FoldersHasPrevious;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearTop(scrollViewer) &&
                   viewModel.FoldersHasPrevious)
            {
                int startOffsetBeforeLoading = viewModel.FolderWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.FolderWindowEndOffset;
                Guid? anchorId = viewModel.Folders.FirstOrDefault()?.Id;

                await viewModel.LoadPreviousFoldersPageCommand.ExecuteAsync(null);
                await WaitForScrollLayoutAsync();

                if (anchorId is Guid id)
                {
                    ScrollFolderAnchorIntoView(sender, viewModel, id);
                    await WaitForScrollLayoutAsync();
                }

                viewModel.UpdateFoldersViewport(
                    GetLogicalEntityOffset(
                        sender,
                        scrollViewer,
                        viewModel.Folders.Count,
                        itemsPerRow));

                if (startOffsetBeforeLoading == viewModel.FolderWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.FolderWindowEndOffset)
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
                   viewModel.LoadNextFoldersPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.FolderWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.FolderWindowEndOffset;
                Guid? anchorId = viewModel.Folders.LastOrDefault()?.Id;

                await viewModel.LoadNextFoldersPageCommand.ExecuteAsync(null);
                await WaitForScrollLayoutAsync();

                if (anchorId is Guid id)
                {
                    ScrollFolderAnchorIntoView(sender, viewModel, id);
                    await WaitForScrollLayoutAsync();
                }

                viewModel.UpdateFoldersViewport(
                    GetLogicalEntityOffset(
                        sender,
                        scrollViewer,
                        viewModel.Folders.Count,
                        itemsPerRow));

                if (startOffsetBeforeLoading == viewModel.FolderWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.FolderWindowEndOffset)
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
        if (DataContext is not LibraryContainerViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);
        if (scrollViewer is null)
        {
            return;
        }

        int itemsPerRow = viewModel.IsTilesView
            ? Math.Max(1, viewModel.ActualMaterialTilesPerRow)
            : viewModel.IsCompactTilesView
                ? Math.Max(1, viewModel.ActualMaterialCompactTilesPerRow)
                : 1;

        viewModel.UpdateMaterialsViewport(
            GetLogicalEntityOffset(
                sender,
                scrollViewer,
                viewModel.Materials.Count,
                itemsPerRow));

        if (_isMaterialsPageLoadRunning ||
            (!IsNearTop(scrollViewer) && !IsNearBottom(scrollViewer)))
        {
            return;
        }

        _isMaterialsPageLoadRunning = true;
        bool loadPreviousPage = IsNearTop(scrollViewer) && viewModel.MaterialsHasPrevious;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearTop(scrollViewer) &&
                   viewModel.MaterialsHasPrevious &&
                   viewModel.LoadPreviousMaterialsPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.MaterialWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.MaterialWindowEndOffset;
                Guid? anchorId = viewModel.Materials.FirstOrDefault()?.Id;

                await viewModel.LoadPreviousMaterialsPageCommand.ExecuteAsync(null);
                await WaitForScrollLayoutAsync();

                if (anchorId is Guid id)
                {
                    ScrollMaterialAnchorIntoView(sender, viewModel, id);
                    await WaitForScrollLayoutAsync();
                }

                viewModel.UpdateMaterialsViewport(
                    GetLogicalEntityOffset(
                        sender,
                        scrollViewer,
                        viewModel.Materials.Count,
                        itemsPerRow));

                if (startOffsetBeforeLoading == viewModel.MaterialWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.MaterialWindowEndOffset)
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
                   viewModel.LoadNextMaterialsPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.MaterialWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.MaterialWindowEndOffset;
                Guid? anchorId = viewModel.Materials.LastOrDefault()?.Id;

                await viewModel.LoadNextMaterialsPageCommand.ExecuteAsync(null);
                await WaitForScrollLayoutAsync();

                if (anchorId is Guid id)
                {
                    ScrollMaterialAnchorIntoView(sender, viewModel, id);
                    await WaitForScrollLayoutAsync();
                }

                viewModel.UpdateMaterialsViewport(
                    GetLogicalEntityOffset(
                        sender,
                        scrollViewer,
                        viewModel.Materials.Count,
                        itemsPerRow));

                if (startOffsetBeforeLoading == viewModel.MaterialWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.MaterialWindowEndOffset)
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
        if (DataContext is not LibraryContainerViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);
        if (scrollViewer is null)
        {
            return;
        }

        int itemsPerRow = viewModel.IsTilesView
            ? Math.Max(1, viewModel.ActualMixedTilesPerRow)
            : viewModel.IsCompactTilesView
                ? Math.Max(1, viewModel.ActualMixedCompactTilesPerRow)
                : 1;

        viewModel.UpdateMixedViewport(
            GetLogicalEntityOffset(
                sender,
                scrollViewer,
                viewModel.MixedContent.Count,
                itemsPerRow));

        if (_isMixedPageLoadRunning ||
            (!IsNearTop(scrollViewer) && !IsNearBottom(scrollViewer)))
        {
            return;
        }

        _isMixedPageLoadRunning = true;
        bool loadPreviousPage = IsNearTop(scrollViewer) && viewModel.MixedHasPrevious;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearTop(scrollViewer) &&
                   viewModel.MixedHasPrevious &&
                   viewModel.LoadPreviousMixedPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.MixedWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.MixedWindowEndOffset;
                Guid? anchorId = viewModel.MixedContent.FirstOrDefault()?.Id;

                await viewModel.LoadPreviousMixedPageCommand.ExecuteAsync(null);
                await WaitForScrollLayoutAsync();

                if (anchorId is Guid id)
                {
                    ScrollMixedAnchorIntoView(sender, viewModel, id);
                    await WaitForScrollLayoutAsync();
                }

                viewModel.UpdateMixedViewport(
                    GetLogicalEntityOffset(
                        sender,
                        scrollViewer,
                        viewModel.MixedContent.Count,
                        itemsPerRow));

                if (startOffsetBeforeLoading == viewModel.MixedWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.MixedWindowEndOffset)
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
                   viewModel.LoadNextMixedPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.MixedWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.MixedWindowEndOffset;
                Guid? anchorId = viewModel.MixedContent.LastOrDefault()?.Id;

                await viewModel.LoadNextMixedPageCommand.ExecuteAsync(null);
                await WaitForScrollLayoutAsync();

                if (anchorId is Guid id)
                {
                    ScrollMixedAnchorIntoView(sender, viewModel, id);
                    await WaitForScrollLayoutAsync();
                }

                viewModel.UpdateMixedViewport(
                    GetLogicalEntityOffset(
                        sender,
                        scrollViewer,
                        viewModel.MixedContent.Count,
                        itemsPerRow));

                if (startOffsetBeforeLoading == viewModel.MixedWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.MixedWindowEndOffset)
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

    private static double GetLogicalEntityOffset(
        object sender,
        ScrollViewer scrollViewer,
        int loadedItemsCount,
        int itemsPerRow)
    {
        if (sender is DataGrid)
        {
            return Math.Max(0, scrollViewer.VerticalOffset);
        }

        int safeItemsPerRow = Math.Max(1, itemsPerRow);
        int loadedRows = Math.Max(1,
            (int)Math.Ceiling(loadedItemsCount / (double)safeItemsPerRow));

        double rowHeight = scrollViewer.ExtentHeight / loadedRows;
        if (rowHeight <= 0 || double.IsNaN(rowHeight) || double.IsInfinity(rowHeight))
        {
            return 0;
        }

        int firstVisibleRow = Math.Max(0,
            (int)Math.Floor(scrollViewer.VerticalOffset / rowHeight));

        return firstVisibleRow * safeItemsPerRow;
    }

    private static void ScrollFolderAnchorIntoView(
        object sender,
        LibraryContainerViewModel viewModel,
        Guid anchorId)
    {
        LibraryFolderCardViewModel? anchor =
            viewModel.Folders.FirstOrDefault(folder => folder.Id == anchorId);

        if (anchor is null)
        {
            return;
        }

        if (sender is DataGrid dataGrid)
        {
            dataGrid.ScrollIntoView(anchor);
            return;
        }

        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        ItemsControl? itemsControl = FindVisualChild<ItemsControl>(scrollViewer);
        FrameworkElement? container =
            itemsControl?.ItemContainerGenerator.ContainerFromItem(anchor) as FrameworkElement;

        container?.BringIntoView();
    }

    private static void ScrollMaterialAnchorIntoView(
        object sender,
        LibraryContainerViewModel viewModel,
        Guid anchorId)
    {
        LibraryMaterialListItemViewModel? anchor =
            viewModel.Materials.FirstOrDefault(material => material.Id == anchorId);

        ScrollAnchorIntoView(sender, anchor);
    }

    private static void ScrollMixedAnchorIntoView(
        object sender,
        LibraryContainerViewModel viewModel,
        Guid anchorId)
    {
        LibraryContentListItemViewModel? anchor =
            viewModel.MixedContent.FirstOrDefault(item => item.Id == anchorId);

        ScrollAnchorIntoView(sender, anchor);
    }

    private static void ScrollAnchorIntoView(object sender, object? anchor)
    {
        if (anchor is null)
        {
            return;
        }

        if (sender is DataGrid dataGrid)
        {
            dataGrid.ScrollIntoView(anchor);
            return;
        }

        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        ItemsControl? itemsControl = FindVisualChild<ItemsControl>(scrollViewer);
        FrameworkElement? container =
            itemsControl?.ItemContainerGenerator.ContainerFromItem(anchor) as FrameworkElement;

        container?.BringIntoView();
    }

    private static bool IsNearTop(ScrollViewer scrollViewer)
    {
        if (scrollViewer.ExtentHeight <= 0 ||
            scrollViewer.ViewportHeight <= 0)
        {
            return false;
        }

        double threshold = Math.Max(
            2,
            scrollViewer.ViewportHeight * 0.5);

        return scrollViewer.VerticalOffset <= threshold;
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
            scrollViewer.ViewportHeight * 0.5);

        return remainingDistance <= threshold;
    }

    private async Task WaitForScrollLayoutAsync()
    {
        await Dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.Background);
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
