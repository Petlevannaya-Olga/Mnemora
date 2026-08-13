using System.Diagnostics;
using System.IO;

namespace Mnemora.Desktop.Storage;

public sealed class FolderLauncherService
    : IFolderLauncherService
{
    public void Open(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException(
                "Путь к папке не указан.",
                nameof(folderPath));
        }

        string fullPath =
            Path.GetFullPath(folderPath);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Папка '{fullPath}' не найдена.");
        }

        Process? process = Process.Start(
            new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true,
            });

        if (process is null)
        {
            throw new InvalidOperationException(
                "Не удалось открыть папку.");
        }
    }
}