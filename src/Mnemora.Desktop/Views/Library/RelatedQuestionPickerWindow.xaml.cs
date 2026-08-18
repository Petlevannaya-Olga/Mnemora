using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

public partial class RelatedQuestionPickerWindow : Window
{
    public RelatedQuestionPickerWindow(
        RelatedQuestionPickerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }

    private RelatedQuestionPickerViewModel ViewModel =>
        (RelatedQuestionPickerViewModel)DataContext;

    private void SelectAll_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        ViewModel.SelectAll();
    }

    private void SelectSection_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid sectionId })
        {
            ViewModel.SelectSection(sectionId);
        }
    }

    private void SelectTopic_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid topicId })
        {
            ViewModel.SelectTopic(topicId);
        }
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

        e.Handled = true;
        DialogResult = false;
    }
}
