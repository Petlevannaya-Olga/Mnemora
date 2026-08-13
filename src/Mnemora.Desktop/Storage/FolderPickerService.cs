using System.IO;
using Microsoft.Win32;

namespace Mnemora.Desktop.Storage;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? SelectFolder(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для материалов Mnemora",
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) &&
            Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        bool? result = dialog.ShowDialog();

        return result == true
            ? dialog.FolderName
            : null;
    }
}