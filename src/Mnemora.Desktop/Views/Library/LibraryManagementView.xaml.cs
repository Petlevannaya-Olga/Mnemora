using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        if (e.ExtentHeight <= 0 || e.ViewportHeight <= 0 ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateSimpleSectionViewport(
            GetLogicalEntityOffset(
                sender,
                e.VerticalOffset,
                viewModel.IsSimpleTilesView ? 3 : 4,
                viewModel.SimpleSectionWindowStartOffset == 0));

        double threshold = Math.Max(2d, e.ViewportHeight * 0.5d);
        bool isNearTop = e.VerticalOffset <= threshold;
        double remainingDistance =
            e.ExtentHeight - e.VerticalOffset - e.ViewportHeight;
        bool isNearBottom = remainingDistance <= threshold;

        if (isNearTop && viewModel.SimpleSectionsHasPrevious)
        {
            Guid? anchorId = viewModel.SimpleSections.FirstOrDefault()?.Id;

            await viewModel.LoadPreviousSimpleSectionWindowAsync(
                _loadCancellationTokenSource?.Token ?? CancellationToken.None);

            if (anchorId is Guid id)
            {
                ScrollSectionAnchorIntoView(sender, viewModel, id);
            }

            return;
        }

        if (!isNearBottom ||
            !viewModel.LoadNextSimpleSectionPageCommand.CanExecute(null))
        {
            return;
        }

        Guid? bottomAnchorId = viewModel.SimpleSections.LastOrDefault()?.Id;

        await viewModel.LoadNextSimpleSectionWindowAsync(
            _loadCancellationTokenSource?.Token ?? CancellationToken.None);

        if (bottomAnchorId is Guid bottomId)
        {
            ScrollSectionAnchorIntoView(sender, viewModel, bottomId);
        }
    }

    private async void TopicsScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0 || e.ViewportHeight <= 0 ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateSimpleTopicViewport(
            GetLogicalEntityOffset(
                sender,
                e.VerticalOffset,
                viewModel.IsSimpleTilesView ? 3 : 4,
                viewModel.SimpleTopicWindowStartOffset == 0));

        double threshold = Math.Max(2d, e.ViewportHeight * 0.5d);
        bool isNearTop = e.VerticalOffset <= threshold;
        double remainingDistance =
            e.ExtentHeight - e.VerticalOffset - e.ViewportHeight;
        bool isNearBottom = remainingDistance <= threshold;

        if (isNearTop && viewModel.SimpleTopicsHasPrevious)
        {
            Guid? anchorId = viewModel.SimpleTopics.FirstOrDefault()?.Id;

            await viewModel.LoadPreviousSimpleTopicWindowAsync(
                _loadCancellationTokenSource?.Token ?? CancellationToken.None);

            if (anchorId is Guid id)
            {
                ScrollTopicAnchorIntoView(sender, viewModel, id);
            }

            return;
        }

        if (!isNearBottom ||
            !viewModel.LoadNextSimpleTopicPageCommand.CanExecute(null))
        {
            return;
        }

        Guid? bottomAnchorId = viewModel.SimpleTopics.LastOrDefault()?.Id;

        await viewModel.LoadNextSimpleTopicWindowAsync(
            _loadCancellationTokenSource?.Token ?? CancellationToken.None);

        if (bottomAnchorId is Guid bottomId)
        {
            ScrollTopicAnchorIntoView(sender, viewModel, bottomId);
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
        if (e.ExtentHeight <= 0 || e.ViewportHeight <= 0 ||
            DataContext is not LibraryManagementViewModel viewModel)
        {
            return;
        }

        // With CanContentScroll=True DataGrid reports logical item offsets,
        // so the footer can show the current 30-item database range rather
        // than the whole 7-page in-memory window.
        viewModel.UpdateSimpleMaterialViewport(e.VerticalOffset);

        double threshold = Math.Max(2d, e.ViewportHeight * 0.5d);
        bool isNearTop = e.VerticalOffset <= threshold;
        double remainingDistance =
            e.ExtentHeight - e.VerticalOffset - e.ViewportHeight;
        bool isNearBottom = remainingDistance <= threshold;

        // Once old pages have been trimmed, reaching the top materializes the
        // previous database page again. The anchor keeps the user's position
        // stable after rows are prepended.
        if (isNearTop && viewModel.SimpleMaterialsHasPrevious)
        {
            Guid? anchorId = viewModel.SimpleMaterials.FirstOrDefault()?.Id;

            await viewModel.LoadPreviousSimpleMaterialWindowAsync(
                _loadCancellationTokenSource?.Token ?? CancellationToken.None);

            if (sender is DataGrid grid && anchorId is Guid anchorMaterialId)
            {
                LibraryManagementOrderItemViewModel? anchor =
                    viewModel.SimpleMaterials.FirstOrDefault(material => material.Id == anchorMaterialId);

                if (anchor is not null)
                {
                    grid.ScrollIntoView(anchor);
                }
            }

            return;
        }

        if (!isNearBottom ||
            !viewModel.LoadNextSimpleMaterialPageCommand.CanExecute(null))
        {
            return;
        }

        Guid? bottomAnchorId = viewModel.SimpleMaterials.LastOrDefault()?.Id;

        await viewModel.LoadNextSimpleMaterialWindowAsync(
            _loadCancellationTokenSource?.Token ?? CancellationToken.None);

        if (sender is DataGrid dataGrid && bottomAnchorId is Guid bottomAnchorMaterialId)
        {
            LibraryManagementOrderItemViewModel? bottomAnchor =
                viewModel.SimpleMaterials.FirstOrDefault(material => material.Id == bottomAnchorMaterialId);

            if (bottomAnchor is not null)
            {
                dataGrid.ScrollIntoView(bottomAnchor);
            }
        }
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
