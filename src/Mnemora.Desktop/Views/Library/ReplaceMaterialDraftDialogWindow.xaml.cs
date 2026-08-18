using System.Windows;
using System.Windows.Input;

namespace Mnemora.Desktop.Views.Library;

public partial class ReplaceMaterialDraftDialogWindow : Window
{
    public ReplaceMaterialDraftDialogWindow(
        string selectedFileName)
    {
        InitializeComponent();

        SelectedFileNameText.Text =
            string.IsNullOrWhiteSpace(selectedFileName)
                ? "Markdown-файл"
                : selectedFileName.Trim();
    }

    private void Confirm_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        DialogResult = false;
        e.Handled = true;
    }
}
