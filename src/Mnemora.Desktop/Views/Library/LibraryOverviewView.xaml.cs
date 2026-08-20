using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "CancellationTokenSource is disposed when the WPF view is unloaded.")]
public partial class LibraryOverviewView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;

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

    private void SectionsScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0 ||
            e.ViewportHeight <= 0 ||
            DataContext is not LibraryOverviewViewModel viewModel)
        {
            return;
        }

        int itemsPerRow = viewModel.IsTilesView ? 3 : 5;
        viewModel.UpdateViewport(
            GetLogicalEntityOffset(sender, e.VerticalOffset, itemsPerRow));

        double remainingDistance = e.ExtentHeight - e.VerticalOffset - e.ViewportHeight;
        double loadingThreshold = Math.Max(2, e.ViewportHeight * 0.5);

        if (remainingDistance > loadingThreshold)
        {
            return;
        }

        if (viewModel.LoadNextPageCommand.CanExecute(null))
        {
            viewModel.LoadNextPageCommand.Execute(null);
        }
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
