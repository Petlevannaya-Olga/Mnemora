namespace Mnemora.Desktop.Dialogs;

public interface IDialogService
{
    TResult Show<TViewModel, TResult>(
        Action<TViewModel>? initialize = null)
        where TViewModel : class, IDialogViewModel<TResult>;
}