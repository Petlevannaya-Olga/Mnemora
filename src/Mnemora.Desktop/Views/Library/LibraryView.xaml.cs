using System.Windows;
using System.Windows.Controls;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

public partial class LibraryView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;

    public LibraryView()
    {
        InitializeComponent();
    }

    private async void LibraryView_OnLoaded(object sender, RoutedEventArgs e)
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        if (DataContext is LibraryViewModel viewModel)
        {
            await viewModel.LoadAsync(_loadCancellationTokenSource.Token);
        }
    }

    private void LibraryView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = null;
    }
}