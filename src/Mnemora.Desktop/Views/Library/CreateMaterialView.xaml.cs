using System.Windows;
using System.Windows.Controls;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView : UserControl
{
    public CreateMaterialView()
    {
        InitializeComponent();
    }

    // Только локальная навигация между макетами шагов.
    // Создание материала и Application-команды здесь не подключены.
    private void GoToBasicStep_OnClick(object sender, RoutedEventArgs e)
    {
        WizardTabs.SelectedIndex = 1;
    }

    private void GoToTypeStep_OnClick(object sender, RoutedEventArgs e)
    {
        WizardTabs.SelectedIndex = 0;
    }

    private void GoToContentStep_OnClick(object sender, RoutedEventArgs e)
    {
        WizardTabs.SelectedIndex = 2;
    }

    private void GoToBasicStepFromContent_OnClick(object sender, RoutedEventArgs e)
    {
        WizardTabs.SelectedIndex = 1;
    }

}
