using Microsoft.Extensions.DependencyInjection;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.Views.Sections;

namespace Mnemora.Desktop.Dialogs;

internal sealed class CreateSectionDialogService(
    IServiceProvider serviceProvider)
    : ICreateSectionDialogService
{
    public Guid? ShowDialog()
    {
        var viewModel = serviceProvider
            .GetRequiredService<CreateSectionDialogViewModel>();

        var dialog = new CreateSectionDialog(viewModel);

        if (System.Windows.Application.Current.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true
            ? dialog.CreatedSectionId
            : null;
    }
}