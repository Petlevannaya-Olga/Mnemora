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
public partial class LibrarySectionView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;

    public LibrarySectionView()
    {
        InitializeComponent();
    }

    private async void LibrarySectionView_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LibrarySectionViewModel viewModel)
        {
            return;
        }

        CancelLoading();

        var cancellationTokenSource = new CancellationTokenSource();
        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            await viewModel.LoadAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            // ignore
        }
        finally
        {
            if (ReferenceEquals(_loadCancellationTokenSource, cancellationTokenSource))
            {
                _loadCancellationTokenSource = null;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void LibrarySectionView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelLoading();
    }

    private void TopicsScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0 || e.ViewportHeight <= 0)
        {
            return;
        }

        double remainingDistance = e.ExtentHeight - e.VerticalOffset - e.ViewportHeight;
        double loadingThreshold = Math.Max(2, e.ViewportHeight * 0.5);

        if (remainingDistance > loadingThreshold)
        {
            return;
        }

        if (DataContext is LibrarySectionViewModel viewModel &&
            viewModel.LoadNextPageCommand.CanExecute(null))
        {
            viewModel.LoadNextPageCommand.Execute(null);
        }
    }

    private void TopicTableRow_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow { DataContext: not null } row ||
            DataContext is not LibrarySectionViewModel viewModel)
        {
            return;
        }

        if (!viewModel.OpenTopicCommand.CanExecute(row.DataContext))
        {
            return;
        }

        viewModel.OpenTopicCommand.Execute(row.DataContext);
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
