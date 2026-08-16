using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
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