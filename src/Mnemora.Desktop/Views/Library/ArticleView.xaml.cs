using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "CancellationTokenSource is disposed when the WPF view is unloaded.")]
public partial class ArticleView : UserControl
{
    private CancellationTokenSource? _loadCancellationTokenSource;

    public ArticleView()
    {
        InitializeComponent();
    }

    private async void ArticleView_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        CancelLoading();

        if (DataContext is not ArticleViewModel viewModel)
        {
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        _loadCancellationTokenSource = cancellationTokenSource;

        try
        {
            await viewModel.LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Уход со страницы отменяет загрузку.
        }
    }

    private void ArticleView_OnUnloaded(
        object sender,
        RoutedEventArgs e) =>
        CancelLoading();

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
