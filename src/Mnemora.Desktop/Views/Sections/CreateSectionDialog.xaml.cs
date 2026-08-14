using System.Windows;
using Mnemora.Desktop.ViewModels.Sections;

namespace Mnemora.Desktop.Views.Sections;

public partial class CreateSectionDialog : Window
{
    private readonly CreateSectionDialogViewModel _viewModel;

    public CreateSectionDialog(CreateSectionDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.SectionCreated += OnSectionCreated;
        _viewModel.CancelRequested += OnCancelRequested;

        Loaded += OnLoaded;
    }

    public Guid? CreatedSectionId { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.Focus();
    }

    private void OnSectionCreated(
        object? sender,
        SectionCreatedEventArgs e)
    {
        CreatedSectionId = e.SectionId;
        DialogResult = true;
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.SectionCreated -= OnSectionCreated;
        _viewModel.CancelRequested -= OnCancelRequested;
        Loaded -= OnLoaded;

        base.OnClosed(e);
    }
}