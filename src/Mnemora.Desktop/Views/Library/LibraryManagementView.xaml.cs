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

    private void SectionsScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0 || e.ViewportHeight <= 0)
        {
            return;
        }

        double remainingDistance =
            e.ExtentHeight - e.VerticalOffset - e.ViewportHeight;

        double loadingThreshold =
            Math.Max(2d, e.ViewportHeight * 0.5d);

        if (remainingDistance > loadingThreshold)
        {
            return;
        }

        if (DataContext is LibraryManagementViewModel viewModel &&
            viewModel.LoadNextSimpleSectionPageCommand.CanExecute(null))
        {
            viewModel.LoadNextSimpleSectionPageCommand.Execute(null);
        }
    }

    private void MaterialsScroll_OnScrollChanged(
        object sender,
        ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0 ||
            e.ViewportHeight <= 0)
        {
            return;
        }

        double remainingDistance =
            e.ExtentHeight -
            e.VerticalOffset -
            e.ViewportHeight;

        double loadingThreshold =
            Math.Max(
                2d,
                e.ViewportHeight * 0.5d);

        if (remainingDistance >
            loadingThreshold)
        {
            return;
        }

        if (DataContext
                is LibraryManagementViewModel viewModel &&
            viewModel.LoadNextSimpleMaterialPageCommand
                .CanExecute(null))
        {
            viewModel.LoadNextSimpleMaterialPageCommand
                .Execute(null);
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
