namespace Mnemora.Desktop.Dialogs;

public interface IDialogViewModel<TResult> : IDialogViewModel
{
    event EventHandler<DialogCloseRequestedEventArgs<TResult>>? CloseRequested;
}

public interface IDialogViewModel
{
    bool IsBusy { get; }

    void CancelPendingOperation();
}