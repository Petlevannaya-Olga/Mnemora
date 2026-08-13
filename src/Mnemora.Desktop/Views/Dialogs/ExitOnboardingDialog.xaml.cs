using System.Windows;

namespace Mnemora.Desktop.Views.Dialogs;

public partial class ExitOnboardingDialog : Window
{
    public ExitOnboardingDialog(
        bool hasPendingApiKey)
    {
        InitializeComponent();

        MessageText.Text = hasPendingApiKey
            ? "Введённый API-ключ хранится только временно и будет потерян. Остальную настройку можно продолжить при следующем запуске."
            : "Вы сможете продолжить настройку Mnemora при следующем запуске приложения.";
    }

    private void ContinueSetup_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Exit_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = true;
    }
}