namespace Mnemora.Desktop.Dialogs;

public sealed class DialogCloseRequestedEventArgs<TResult>(
    TResult result,
    bool isConfirmed)
    : EventArgs
{
    public TResult Result { get; } = result;

    public bool IsConfirmed { get; } = isConfirmed;
}