using System.IO;
using Microsoft.Extensions.Logging;

namespace Mnemora.Desktop.Startup;

public sealed class LocalAppDataCleanupService(IMnemoraLocalPathProvider pathProvider, ILogger<LocalAppDataCleanupService> logger) : ILocalAppDataCleanupService
{
    public Task<LocalAppDataCleanupReport> CleanupAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CleanupCore(cancellationToken), cancellationToken);
    }

    private LocalAppDataCleanupReport CleanupCore(CancellationToken cancellationToken)
    {
        int deletedCount = 0;
        int skippedCount = 0;

        CleanupTempDirectory(
            pathProvider.TempPath,
            ref deletedCount,
            ref skippedCount,
            cancellationToken);

        return new LocalAppDataCleanupReport(deletedCount, skippedCount);
    }

    private void CleanupTempDirectory(
        string directoryPath,
        ref int deletedCount,
        ref int skippedCount,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(directoryPath);
        }
        catch (DirectoryNotFoundException)
        {
            // Каталог уже удалён параллельно — очищать больше нечего.
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skippedCount++;
            logger.LogWarning(exception, "Не удалось прочитать временный каталог Mnemora {DirectoryPath}", directoryPath);
            return;
        }

        bool hasSkippedEntries = false;

        foreach (string entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, true);
                }
                else
                {
                    File.Delete(entry);
                }

                deletedCount++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                hasSkippedEntries = true;
                skippedCount++;
                logger.LogWarning(exception, "Не удалось удалить временный объект Mnemora {EntryPath}", entry);
            }
        }

        if (hasSkippedEntries)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Directory.Delete(directoryPath, recursive: false);
        }
        catch (DirectoryNotFoundException)
        {
            // Каталог уже удалён параллельно — требуемое состояние достигнуто.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skippedCount++;
            logger.LogWarning(
                exception,
                "Не удалось удалить временный каталог Mnemora {DirectoryPath}",
                directoryPath);
        }
    }
}
