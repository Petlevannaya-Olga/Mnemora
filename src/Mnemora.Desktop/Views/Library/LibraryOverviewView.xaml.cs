using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "CancellationTokenSource is disposed when the WPF view is unloaded.")]
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

        if (DataContext is LibraryOverviewViewModel viewModel &&
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