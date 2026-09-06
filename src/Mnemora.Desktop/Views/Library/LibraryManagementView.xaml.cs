using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Mnemora.Application.Library.Order;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "CancellationTokenSource is disposed when the WPF view is unloaded.")]
public partial class LibraryManagementView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;
    private bool _isSectionsScrollPageLoadRunning;
    private bool _isTopicsScrollPageLoadRunning;
    private bool _isMaterialsScrollPageLoadRunning;
    private bool _isSectionStructureMaterialsScrollPageLoadRunning;
    private bool _isSectionStructureTreeSelectionRunning;
    private bool _isSectionStructureTreePageLoadRunning;

    public LibraryManagementView()
    {
        InitializeComponent();
    }

    private async void LibraryManagementView_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        CancelLoading();

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            if (DataContext is LibraryManagementViewModel viewModel)
            {
                await viewModel.LoadAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // View was unloaded while the library was loading.
        }
    }

    private void LibraryManagementView_OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        CancelLoading();
    }

    private async void SectionsScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);

        if (scrollViewer is null ||
            scrollViewer.ExtentHeight <= 0 ||
            scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        viewModel.UpdateSimpleSectionViewport(
            GetLogicalEntityOffset(
                sender,
                scrollViewer.VerticalOffset,
                viewModel.IsSimpleTilesView ? 3 : 4,
                viewModel.SimpleSectionWindowStartOffset == 0));

        if (_isSectionsScrollPageLoadRunning ||
            (!IsNearTop(scrollViewer) && !IsNearBottom(scrollViewer)))
        {
            return;
        }

        _isSectionsScrollPageLoadRunning = true;
        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;
        bool loadPreviousPage =
            IsNearTop(scrollViewer) && viewModel.SimpleSectionsHasPrevious;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearTop(scrollViewer) &&
                   viewModel.SimpleSectionsHasPrevious)
            {
                int startOffsetBeforeLoading = viewModel.SimpleSectionWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.SimpleSectionWindowEndOffset;
                Guid? anchorId = viewModel.SimpleSections.FirstOrDefault()?.Id;

                await viewModel.LoadPreviousSimpleSectionWindowAsync(cancellationToken);

                if (anchorId is Guid id)
                {
                    ScrollSectionAnchorIntoView(sender, viewModel, id);
                }

                await WaitForScrollLayoutAsync();

                if (startOffsetBeforeLoading == viewModel.SimpleSectionWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.SimpleSectionWindowEndOffset)
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
                   viewModel.LoadNextSimpleSectionPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.SimpleSectionWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.SimpleSectionWindowEndOffset;
                Guid? anchorId = viewModel.SimpleSections.LastOrDefault()?.Id;

                await viewModel.LoadNextSimpleSectionWindowAsync(cancellationToken);

                if (anchorId is Guid id)
                {
                    ScrollSectionAnchorIntoView(sender, viewModel, id);
                }

                await WaitForScrollLayoutAsync();

                if (startOffsetBeforeLoading == viewModel.SimpleSectionWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.SimpleSectionWindowEndOffset)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Обычная отмена при уходе со страницы.
        }
        finally
        {
            _isSectionsScrollPageLoadRunning = false;
        }
    }

    private async void TopicsScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);

        if (scrollViewer is null ||
            scrollViewer.ExtentHeight <= 0 ||
            scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        viewModel.UpdateSimpleTopicViewport(
            GetLogicalEntityOffset(
                sender,
                scrollViewer.VerticalOffset,
                viewModel.IsSimpleTilesView ? 3 : 4,
                viewModel.SimpleTopicWindowStartOffset == 0));

        if (_isTopicsScrollPageLoadRunning ||
            (!IsNearTop(scrollViewer) && !IsNearBottom(scrollViewer)))
        {
            return;
        }

        _isTopicsScrollPageLoadRunning = true;
        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;
        bool loadPreviousPage =
            IsNearTop(scrollViewer) && viewModel.SimpleTopicsHasPrevious;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearTop(scrollViewer) &&
                   viewModel.SimpleTopicsHasPrevious)
            {
                int startOffsetBeforeLoading = viewModel.SimpleTopicWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.SimpleTopicWindowEndOffset;
                Guid? anchorId = viewModel.SimpleTopics.FirstOrDefault()?.Id;

                await viewModel.LoadPreviousSimpleTopicWindowAsync(cancellationToken);

                if (anchorId is Guid id)
                {
                    ScrollTopicAnchorIntoView(sender, viewModel, id);
                }

                await WaitForScrollLayoutAsync();

                if (startOffsetBeforeLoading == viewModel.SimpleTopicWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.SimpleTopicWindowEndOffset)
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
                   viewModel.LoadNextSimpleTopicPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.SimpleTopicWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.SimpleTopicWindowEndOffset;
                Guid? anchorId = viewModel.SimpleTopics.LastOrDefault()?.Id;

                await viewModel.LoadNextSimpleTopicWindowAsync(cancellationToken);

                if (anchorId is Guid id)
                {
                    ScrollTopicAnchorIntoView(sender, viewModel, id);
                }

                await WaitForScrollLayoutAsync();

                if (startOffsetBeforeLoading == viewModel.SimpleTopicWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.SimpleTopicWindowEndOffset)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Обычная отмена при уходе со страницы.
        }
        finally
        {
            _isTopicsScrollPageLoadRunning = false;
        }
    }

    private static double GetLogicalEntityOffset(
        object sender,
        double verticalOffset,
        int itemsPerRow,
        bool firstRowContainsCreateTile)
    {
        if (sender is DataGrid)
        {
            return verticalOffset;
        }

        int rowIndex = Math.Max(0, (int)Math.Floor(verticalOffset));
        int itemIndex = rowIndex * Math.Max(1, itemsPerRow);

        if (firstRowContainsCreateTile && rowIndex > 0)
        {
            itemIndex--;
        }

        return Math.Max(0, itemIndex);
    }

    private static void ScrollSectionAnchorIntoView(
        object sender,
        LibraryManagementViewModel viewModel,
        Guid anchorId)
    {
        if (sender is DataGrid grid)
        {
            LibraryManagementSectionViewModel? item =
                viewModel.SimpleSections.FirstOrDefault(section => section.Id == anchorId);

            if (item is not null)
            {
                grid.ScrollIntoView(item);
            }

            return;
        }

        if (sender is ListBox listBox)
        {
            IEnumerable<LibraryManagementSectionRowViewModel> rows =
                ReferenceEquals(listBox.ItemsSource, viewModel.SimpleCompactSectionRows)
                    ? viewModel.SimpleCompactSectionRows
                    : viewModel.SimpleSectionRows;

            LibraryManagementSectionRowViewModel? row = rows.FirstOrDefault(
                candidate => candidate.Sections.Any(section => section.Id == anchorId));

            if (row is not null)
            {
                listBox.ScrollIntoView(row);
            }
        }
    }

    private static void ScrollTopicAnchorIntoView(
        object sender,
        LibraryManagementViewModel viewModel,
        Guid anchorId)
    {
        if (sender is DataGrid grid)
        {
            LibraryManagementOrderItemViewModel? item =
                viewModel.SimpleTopics.FirstOrDefault(topic => topic.Id == anchorId);

            if (item is not null)
            {
                grid.ScrollIntoView(item);
            }

            return;
        }

        if (sender is ListBox listBox)
        {
            IEnumerable<LibraryManagementTopicRowViewModel> rows =
                ReferenceEquals(listBox.ItemsSource, viewModel.SimpleCompactTopicRows)
                    ? viewModel.SimpleCompactTopicRows
                    : viewModel.SimpleTopicRows;

            LibraryManagementTopicRowViewModel? row = rows.FirstOrDefault(
                candidate => candidate.Topics.Any(topic => topic.Id == anchorId));

            if (row is not null)
            {
                listBox.ScrollIntoView(row);
            }
        }
    }

    private async void MaterialsScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);

        if (scrollViewer is null ||
            scrollViewer.ExtentHeight <= 0 ||
            scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        // With CanContentScroll=True DataGrid reports logical item offsets,
        // so the footer can show the current 30-item database range rather
        // than the whole 7-page in-memory window.
        viewModel.UpdateSimpleMaterialViewport(scrollViewer.VerticalOffset);

        if (_isMaterialsScrollPageLoadRunning ||
            (!IsNearTop(scrollViewer) && !IsNearBottom(scrollViewer)))
        {
            return;
        }

        _isMaterialsScrollPageLoadRunning = true;
        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;
        bool loadPreviousPage =
            IsNearTop(scrollViewer) && viewModel.SimpleMaterialsHasPrevious;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearTop(scrollViewer) &&
                   viewModel.SimpleMaterialsHasPrevious)
            {
                int startOffsetBeforeLoading = viewModel.SimpleMaterialWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.SimpleMaterialWindowEndOffset;
                Guid? anchorId = viewModel.SimpleMaterials.FirstOrDefault()?.Id;

                await viewModel.LoadPreviousSimpleMaterialWindowAsync(cancellationToken);

                if (anchorId is Guid id)
                {
                    ScrollMaterialAnchorIntoView(sender, viewModel, id);
                }

                await WaitForScrollLayoutAsync();

                if (startOffsetBeforeLoading == viewModel.SimpleMaterialWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.SimpleMaterialWindowEndOffset)
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
                   viewModel.LoadNextSimpleMaterialPageCommand.CanExecute(null))
            {
                int startOffsetBeforeLoading = viewModel.SimpleMaterialWindowStartOffset;
                int endOffsetBeforeLoading = viewModel.SimpleMaterialWindowEndOffset;
                Guid? anchorId = viewModel.SimpleMaterials.LastOrDefault()?.Id;

                await viewModel.LoadNextSimpleMaterialWindowAsync(cancellationToken);

                if (anchorId is Guid id)
                {
                    ScrollMaterialAnchorIntoView(sender, viewModel, id);
                }

                await WaitForScrollLayoutAsync();

                if (startOffsetBeforeLoading == viewModel.SimpleMaterialWindowStartOffset &&
                    endOffsetBeforeLoading == viewModel.SimpleMaterialWindowEndOffset)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Обычная отмена при уходе со страницы.
        }
        finally
        {
            _isMaterialsScrollPageLoadRunning = false;
        }
    }

    private async void SectionStructureTreeItem_OnExpanded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not TreeViewItem { DataContext: LibrarySectionManagementTreeNodeViewModel node } ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        if (e.OriginalSource is TreeViewItem original && !ReferenceEquals(sender, original))
        {
            return;
        }

        e.Handled = true;
        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            await viewModel.SectionStructure.ExpandAsync(node, cancellationToken);
            await WaitForScrollLayoutAsync();

            TreeView? treeView = FindAncestor<TreeView>(sender as DependencyObject);
            if (treeView is not null)
            {
                await LoadVisibleSectionStructureFolderPagesAsync(
                    treeView,
                    viewModel,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // View was unloaded or another navigation replaced the current load.
        }
    }

    private async void SectionStructureTree_OnSelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (_isSectionStructureTreeSelectionRunning ||
            e.NewValue is not LibrarySectionManagementTreeNodeViewModel node ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;
        _isSectionStructureTreeSelectionRunning = true;

        try
        {
            if (node.IsLoadMore || node.IsPlaceholder)
            {
                return;
            }

            if (node.IsError)
            {
                await viewModel.SectionStructure.RetryFoldersAsync(node, cancellationToken);
                return;
            }

            if (!node.IsPlaceholder)
            {
                await viewModel.SectionStructure.SelectNodeAsync(node, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // View was unloaded or another navigation replaced the current load.
        }
        finally
        {
            _isSectionStructureTreeSelectionRunning = false;
        }
    }

    private async void SectionStructureTree_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (sender is not TreeView treeView ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            await LoadVisibleSectionStructureFolderPagesAsync(
                treeView,
                viewModel,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // View was unloaded or navigation replaced the current load.
        }
    }

    private async Task LoadVisibleSectionStructureFolderPagesAsync(
        TreeView treeView,
        LibraryManagementViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (_isSectionStructureTreePageLoadRunning)
        {
            return;
        }

        _isSectionStructureTreePageLoadRunning = true;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   !cancellationToken.IsCancellationRequested)
            {
                LibrarySectionManagementTreeNodeViewModel? loaderNode =
                    FindVisibleSectionStructureLoader(treeView, treeView);

                if (loaderNode is null)
                {
                    break;
                }

                await viewModel.SectionStructure.LoadMoreFoldersAsync(
                    loaderNode,
                    cancellationToken);

                await WaitForScrollLayoutAsync();
            }
        }
        finally
        {
            _isSectionStructureTreePageLoadRunning = false;
        }
    }

    private static LibrarySectionManagementTreeNodeViewModel? FindVisibleSectionStructureLoader(
        DependencyObject parent,
        TreeView treeView)
    {
        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

        for (int index = 0; index < childrenCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);

            if (child is TreeViewItem item &&
                item.DataContext is LibrarySectionManagementTreeNodeViewModel node &&
                node.IsLoadMore &&
                node.Parent is { IsLoading: false } &&
                IsVisibleInsideTree(item, treeView))
            {
                return node;
            }

            LibrarySectionManagementTreeNodeViewModel? nested =
                FindVisibleSectionStructureLoader(child, treeView);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool IsVisibleInsideTree(
        FrameworkElement element,
        FrameworkElement treeView)
    {
        if (!element.IsVisible ||
            element.ActualWidth <= 0 ||
            element.ActualHeight <= 0 ||
            treeView.ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            Rect bounds = element
                .TransformToAncestor(treeView)
                .TransformBounds(
                    new Rect(
                        0,
                        0,
                        element.ActualWidth,
                        element.ActualHeight));

            return bounds.Bottom >= 0 &&
                   bounds.Top <= treeView.ActualHeight;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async void SectionStructureMaterialsScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        LibrarySectionManagementViewModel sectionViewModel = viewModel.SectionStructure;
        ScrollViewer? scrollViewer = ResolveScrollViewer(sender, e);

        if (scrollViewer is null ||
            scrollViewer.ExtentHeight <= 0 ||
            scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        sectionViewModel.UpdateMaterialsViewport(scrollViewer.VerticalOffset);

        if (_isSectionStructureMaterialsScrollPageLoadRunning ||
            (!IsNearTop(scrollViewer) && !IsNearBottom(scrollViewer)))
        {
            return;
        }

        _isSectionStructureMaterialsScrollPageLoadRunning = true;
        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;
        bool loadPreviousPage =
            IsNearTop(scrollViewer) && sectionViewModel.MaterialsHasPrevious;

        try
        {
            while (IsLoaded &&
                   ReferenceEquals(DataContext, viewModel) &&
                   IsNearTop(scrollViewer) &&
                   sectionViewModel.MaterialsHasPrevious)
            {
                int startOffsetBeforeLoading = sectionViewModel.MaterialsWindowStartOffset;
                int endOffsetBeforeLoading = sectionViewModel.MaterialsWindowEndOffset;
                Guid? anchorId = sectionViewModel.Materials.FirstOrDefault()?.Id;

                await sectionViewModel.LoadPreviousMaterialsWindowAsync(cancellationToken);

                if (anchorId is Guid id)
                {
                    ScrollSectionStructureMaterialAnchorIntoView(sender, sectionViewModel, id);
                }

                await WaitForScrollLayoutAsync();

                if (startOffsetBeforeLoading == sectionViewModel.MaterialsWindowStartOffset &&
                    endOffsetBeforeLoading == sectionViewModel.MaterialsWindowEndOffset)
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
                   sectionViewModel.MaterialsHasMore)
            {
                int startOffsetBeforeLoading = sectionViewModel.MaterialsWindowStartOffset;
                int endOffsetBeforeLoading = sectionViewModel.MaterialsWindowEndOffset;
                Guid? anchorId = sectionViewModel.Materials.LastOrDefault()?.Id;

                await sectionViewModel.LoadNextMaterialsWindowAsync(cancellationToken);

                if (anchorId is Guid id)
                {
                    ScrollSectionStructureMaterialAnchorIntoView(sender, sectionViewModel, id);
                }

                await WaitForScrollLayoutAsync();

                if (startOffsetBeforeLoading == sectionViewModel.MaterialsWindowStartOffset &&
                    endOffsetBeforeLoading == sectionViewModel.MaterialsWindowEndOffset)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation when leaving the page.
        }
        finally
        {
            _isSectionStructureMaterialsScrollPageLoadRunning = false;
        }
    }

    private static void ScrollSectionStructureMaterialAnchorIntoView(
        object sender,
        LibrarySectionManagementViewModel viewModel,
        Guid anchorId)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        LibraryManagementOrderItemViewModel? anchor =
            viewModel.Materials.FirstOrDefault(material => material.Id == anchorId);

        if (anchor is not null)
        {
            grid.ScrollIntoView(anchor);
        }
    }

    private void SimpleSectionsTable_OnSizeChanged(
        object sender,
        SizeChangedEventArgs e)
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

    private void SimpleSectionTableRow_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row ||
            row.DataContext is not LibraryManagementSectionViewModel section ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source &&
            FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (viewModel.OpenSimpleSectionCommand.CanExecute(section))
        {
            viewModel.OpenSimpleSectionCommand.Execute(section);
            e.Handled = true;
        }
    }

    private void SimpleTopicTableRow_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row ||
            row.DataContext is not LibraryManagementOrderItemViewModel topic ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source &&
            FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (viewModel.OpenSimpleTopicCommand.CanExecute(topic))
        {
            viewModel.OpenSimpleTopicCommand.Execute(topic);
            e.Handled = true;
        }
    }

    private async void ConfigureSectionsOrder_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        await ShowOrderDialogAsync(LibraryOrderTarget.Sections);
    }

    private async void ConfigureTopicsOrder_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        await ShowOrderDialogAsync(LibraryOrderTarget.Topics);
    }

    private async void ConfigureMaterialsOrder_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        await ShowOrderDialogAsync(LibraryOrderTarget.Materials);
    }

    private async Task ShowOrderDialogAsync(LibraryOrderTarget target)
    {
        if (DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        CancellationToken cancellationToken =
            _loadCancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            IReadOnlyList<LibraryManagementOrderItemViewModel> items =
                await viewModel.LoadOrderItemsForDialogAsync(
                    target,
                    cancellationToken);

            if (items.Count == 0 ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            string? contextName = target switch
            {
                LibraryOrderTarget.Topics => viewModel.SelectedSection?.Name,
                LibraryOrderTarget.Materials => viewModel.SelectedTopic?.Name,
                _ => null,
            };

            var dialog = new LibraryOrderDialogWindow(
                target,
                items,
                contextName);

            Window? owner = Window.GetWindow(this);

            if (owner is not null)
            {
                dialog.Owner = owner;
            }

            var overlayHost =
                System.Windows.Application.Current.MainWindow as IDialogOverlayHost;

            bool? dialogResult;

            overlayHost?.ShowDialogOverlay();

            try
            {
                dialogResult = dialog.ShowDialog();
            }
            finally
            {
                overlayHost?.HideDialogOverlay();
            }

            if (dialogResult != true)
            {
                return;
            }

            bool wasSaved = await viewModel.SaveOrderFromDialogAsync(
                target,
                dialog.OrderedIds,
                cancellationToken);

            if (!wasSaved &&
                !cancellationToken.IsCancellationRequested)
            {
                string message =
                    viewModel.ErrorMessage ?? "Не удалось сохранить порядок.";

                if (owner is not null)
                {
                    MessageBox.Show(
                        owner,
                        message,
                        "Mnemora",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        message,
                        "Mnemora",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // View was unloaded while the order dialog was being prepared/saved.
        }
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

    private static bool IsNearTop(ScrollViewer scrollViewer)
    {
        double threshold = Math.Max(2d, scrollViewer.ViewportHeight * 0.5d);
        return scrollViewer.VerticalOffset <= threshold;
    }

    private static bool IsNearBottom(ScrollViewer scrollViewer)
    {
        double remainingDistance = Math.Max(
            0d,
            scrollViewer.ExtentHeight -
            scrollViewer.VerticalOffset -
            scrollViewer.ViewportHeight);

        double threshold = Math.Max(2d, scrollViewer.ViewportHeight * 0.5d);
        return remainingDistance <= threshold;
    }

    private async Task WaitForScrollLayoutAsync()
    {
        await Dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.Background);
    }

    private static void ScrollMaterialAnchorIntoView(
        object sender,
        LibraryManagementViewModel viewModel,
        Guid anchorId)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        LibraryManagementOrderItemViewModel? anchor =
            viewModel.SimpleMaterials.FirstOrDefault(material => material.Id == anchorId);

        if (anchor is not null)
        {
            grid.ScrollIntoView(anchor);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        DependencyObject? current = source;

        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
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
