using System.IO;
using System.Security;
using Microsoft.Extensions.Logging;

namespace Mnemora.Desktop.Startup;

public sealed class StorageTemporaryFilesCleanupService(
    ILogger<StorageTemporaryFilesCleanupService> logger)
    : IStorageTemporaryFilesCleanupService
{
    private const string MaterialsDirectoryName =
        "materials";

    // Только технические рабочие файлы. Полноценные материалы
    // со статусом Draft хранятся в обычных каталогах материалов.
    private const string DraftsDirectoryName =
        "_drafts";

    public Task<StorageTemporaryFilesCleanupReport> CleanupAsync(
        string? storagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.FromResult(
                new StorageTemporaryFilesCleanupReport(0, 0));
        }

        return Task.Run(
            () => CleanupCore(
                storagePath,
                cancellationToken),
            cancellationToken);
    }

    private StorageTemporaryFilesCleanupReport CleanupCore(
        string storagePath,
        CancellationToken cancellationToken)
    {
        int deletedCount = 0;
        int skippedCount = 0;

        string rootPath;

        try
        {
            rootPath = Path.GetFullPath(
                storagePath.Trim());
        }
        catch (Exception exception)
            when (exception is ArgumentException
                      or NotSupportedException
                      or PathTooLongException
                      or SecurityException)
        {
            logger.LogWarning(
                exception,
                "Не удалось определить путь хранилища для очистки временных файлов");

            return new StorageTemporaryFilesCleanupReport(0, 1);
        }

        CleanupDirectory(
            Path.Combine(
                rootPath,
                MaterialsDirectoryName,
                DraftsDirectoryName),
            ref deletedCount,
            ref skippedCount,
            cancellationToken);

        return new StorageTemporaryFilesCleanupReport(
            deletedCount,
            skippedCount);
    }

    private void CleanupDirectory(
        string directoryPath,
        ref int deletedCount,
        ref int skippedCount,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            FileAttributes rootAttributes =
                File.GetAttributes(directoryPath);

            // Не заходим в junction/symlink: удаляем только саму ссылку,
            // чтобы очистка не могла затронуть данные вне хранилища.
            if (rootAttributes.HasFlag(
                    FileAttributes.ReparsePoint))
            {
                Directory.Delete(
                    directoryPath,
                    recursive: false);

                deletedCount++;
                return;
            }
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException)
        {
            skippedCount++;

            logger.LogWarning(
                exception,
                "Не удалось прочитать временный каталог хранилища Mnemora {DirectoryPath}",
                directoryPath);

            return;
        }

        if (!TryDeleteDirectoryTree(
                directoryPath,
                ref deletedCount,
                ref skippedCount,
                cancellationToken))
        {
            return;
        }
    }

    private bool TryDeleteDirectoryTree(
        string directoryPath,
        ref int deletedCount,
        ref int skippedCount,
        CancellationToken cancellationToken)
    {
        string[] entries;

        try
        {
            entries = Directory.GetFileSystemEntries(
                directoryPath);
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException)
        {
            skippedCount++;

            logger.LogWarning(
                exception,
                "Не удалось прочитать временный каталог хранилища Mnemora {DirectoryPath}",
                directoryPath);

            return false;
        }

        bool allEntriesDeleted = true;

        foreach (string entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryDeleteEntry(
                    entry,
                    ref deletedCount,
                    ref skippedCount,
                    cancellationToken))
            {
                allEntriesDeleted = false;
            }
        }

        if (!allEntriesDeleted)
        {
            return false;
        }

        try
        {
            Directory.Delete(
                directoryPath,
                recursive: false);

            return true;
        }
        catch (DirectoryNotFoundException)
        {
            // Требуемое состояние уже достигнуто.
            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException)
        {
            skippedCount++;

            logger.LogWarning(
                exception,
                "Не удалось удалить временный каталог хранилища Mnemora {DirectoryPath}",
                directoryPath);

            return false;
        }
    }

    private bool TryDeleteEntry(
        string entryPath,
        ref int deletedCount,
        ref int skippedCount,
        CancellationToken cancellationToken)
    {
        try
        {
            FileAttributes attributes =
                File.GetAttributes(entryPath);

            bool isDirectory =
                attributes.HasFlag(
                    FileAttributes.Directory);

            bool isReparsePoint =
                attributes.HasFlag(
                    FileAttributes.ReparsePoint);

            if (isDirectory && !isReparsePoint)
            {
                return TryDeleteDirectoryTree(
                    entryPath,
                    ref deletedCount,
                    ref skippedCount,
                    cancellationToken);
            }

            if (isDirectory)
            {
                Directory.Delete(
                    entryPath,
                    recursive: false);
            }
            else
            {
                File.Delete(entryPath);
            }

            deletedCount++;
            return true;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException)
        {
            skippedCount++;

            logger.LogWarning(
                exception,
                "Не удалось удалить временный объект хранилища Mnemora {EntryPath}",
                entryPath);

            return false;
        }
    }
}
