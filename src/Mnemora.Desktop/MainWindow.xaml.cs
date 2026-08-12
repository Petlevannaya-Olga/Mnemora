using System.Windows;
using Mnemora.Desktop.ViewModels.Shell;

namespace Mnemora.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(
        MainWindowViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}