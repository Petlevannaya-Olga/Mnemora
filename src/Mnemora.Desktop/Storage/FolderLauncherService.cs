using System.Diagnostics;
using System.IO;

namespace Mnemora.Desktop.Storage;

public sealed class FolderLauncherService
    : IFolderLauncherService
{
    public void Open(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(
                $"Папка хранилища '{folderPath}' не найдена.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
    }
}