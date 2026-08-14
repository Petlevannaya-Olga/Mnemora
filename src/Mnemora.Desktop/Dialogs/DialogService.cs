using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Mnemora.Desktop.Dialogs;

internal sealed class DialogService(
    IServiceScopeFactory scopeFactory)
    : IDialogService
{
    public TResult Show<TViewModel, TResult>(
        Action<TViewModel>? initialize = null)
        where TViewModel : class, IDialogViewModel<TResult>
    {
        using var scope = scopeFactory.CreateScope();

        var viewModel = scope.ServiceProvider
            .GetRequiredService<TViewModel>();

        initialize?.Invoke(viewModel);

        var result = default(TResult)!;

        var dialog = new DialogWindow
        {
            DataContext = viewModel
        };

        if (System.Windows.Application.Current?.MainWindow is { } owner)
        {
            dialog.Owner = owner;
        }

        void OnCloseRequested(
            object? sender,
            DialogCloseRequestedEventArgs<TResult> eventArgs)
        {
            result = eventArgs.Result;
            dialog.DialogResult = eventArgs.IsConfirmed;
        }

        viewModel.CloseRequested += OnCloseRequested;

        try
        {
            dialog.ShowDialog();
            return result;
        }
        finally
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
    }
}