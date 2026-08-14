using System.Windows;
using System.Windows.Controls;
using Mnemora.Desktop.ViewModels.Home;

namespace Mnemora.Desktop.Views.Home;

public partial class HomeView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;

    public HomeView()
    {
        InitializeComponent();
    }

    private async void HomeView_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel)
        {
            return;
        }

        _loadCancellationTokenSource?.Cancel();

        var cancellationTokenSource =
            new CancellationTokenSource();

        _loadCancellationTokenSource =
            cancellationTokenSource;

        try
        {
            await viewModel.LoadAsync(
                cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource.IsCancellationRequested)
        {
            // ignore
        }
        finally
        {
            if (ReferenceEquals(
                    _loadCancellationTokenSource,
                    cancellationTokenSource))
            {
                _loadCancellationTokenSource = null;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private void HomeView_OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource = null;
    }
}