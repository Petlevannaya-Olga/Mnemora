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

        CleanupDirectory(pathProvider.TempPath, ref deletedCount, ref skippedCount, cancellationToken);
        CleanupDirectory(pathProvider.StagingPath, ref deletedCount, ref skippedCount, cancellationToken);

        return new LocalAppDataCleanupReport(deletedCount, skippedCount);
    }

    private void CleanupDirectory(string directoryPath, ref int deletedCount, ref int skippedCount, CancellationToken cancellationToken)
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            skippedCount++;
            logger.LogWarning(exception, "Не удалось прочитать временный каталог Mnemora {DirectoryPath}", directoryPath);
            return;
        }

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
                skippedCount++;
                logger.LogWarning(exception, "Не удалось удалить временный объект Mnemora {EntryPath}", entry);
            }
        }
    }
}
