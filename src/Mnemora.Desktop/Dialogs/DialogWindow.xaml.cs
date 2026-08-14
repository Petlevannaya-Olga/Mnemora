using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Mnemora.Desktop.Dialogs;

public partial class DialogWindow : Window
{
    public DialogWindow()
    {
        InitializeComponent();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        if (DataContext is IDialogViewModel
            {
                IsBusy: true
            } viewModel)
        {
            viewModel.CancelPendingOperation();
        }
        else
        {
            Close();
        }

        e.Handled = true;

        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DialogResult != true
            && DataContext is IDialogViewModel
            {
                IsBusy: true
            } viewModel)
        {
            viewModel.CancelPendingOperation();
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}