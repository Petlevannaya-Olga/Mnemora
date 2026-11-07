using System.IO;
using System.Security;
using System.Text.Json;
using Mnemora.Desktop.Startup;

namespace Mnemora.Desktop.Storage;

public sealed class StorageValidationService(
    IMnemoraLocalPathProvider localPathProvider)
    : IStorageValidationService
{
    private const string STORAGE_MARKER_FILE_NAME =
        ".mnemora";

    private const int CURRENT_STORAGE_FORMAT_VERSION = 1;

    private const string STORAGE_MARKER_CONTENT =
        "{\"formatVersion\":1}";

    private const string OBSIDIAN_METADATA_DIRECTORY_NAME =
        ".obsidian";

    private const string RECOVERY_DIRECTORY_NAME =
        "Recovery";

    public StorageValidationService()
        : this(new MnemoraLocalPathProvider())
    {
    }

    public StorageValidationResult ValidateCandidate(
        string? storagePath)
    {
        StorageValidationResult pathResult =
            ValidateExistingDirectory(storagePath);

        if (!pathResult.IsValid)
        {
            return pathResult;
        }

        string normalizedPath =
            pathResult.NormalizedPath!;

        try
        {
            bool isEmpty =
                !Directory
                    .EnumerateFileSystemEntries(
                        normalizedPath)
                    .Any();

            if (!isEmpty)
            {
                StorageValidationResult markerResult =
                    ValidateMarker(normalizedPath);

                if (!markerResult.IsValid)
                {
                    return markerResult;
                }
            }

            return CanWriteToDirectory(normalizedPath)
                ? StorageValidationResult.Success(
                    normalizedPath)
                : StorageValidationResult.Failure(
                    "Нет доступа на запись в выбранную папку хранилища.");
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException
                      or NotSupportedException)
        {
            return StorageValidationResult.Failure(
                "Не удалось проверить содержимое выбранной папки хранилища.");
        }
    }

    public StorageValidationResult ValidateConfigured(
        string? storagePath)
    {
        StorageValidationResult pathResult =
            ValidateExistingDirectory(storagePath);

        if (!pathResult.IsValid)
        {
            return pathResult;
        }

        string normalizedPath =
            pathResult.NormalizedPath!;

        StorageValidationResult markerResult =
            ValidateMarker(normalizedPath);

        if (!markerResult.IsValid)
        {
            return markerResult;
        }

        return CanWriteToDirectory(normalizedPath)
            ? StorageValidationResult.Success(
                normalizedPath)
            : StorageValidationResult.Failure(
                "Нет доступа на запись в папку хранилища Mnemora.");
    }

    public async Task<StorageValidationResult> PrepareAsync(
        string? storagePath,
        CancellationToken cancellationToken = default)
    {
        StorageValidationResult pathResult =
            ValidateExistingDirectory(storagePath);

        if (!pathResult.IsValid)
        {
            return pathResult;
        }

        string normalizedPath =
            pathResult.NormalizedPath!;

        StorageValidationResult candidateResult =
            ValidateCandidate(normalizedPath);

        if (!candidateResult.IsValid)
        {
            if (IsMarkerFailure(
                    candidateResult.FailureKind) &&
                ContainsNoMnemoraData(
                    normalizedPath))
            {
                return await WriteCurrentMarkerAsync(
                    normalizedPath,
                    cancellationToken);
            }

            return candidateResult;
        }

        string markerPath = Path.Combine(
            normalizedPath,
            STORAGE_MARKER_FILE_NAME);

        if (!File.Exists(markerPath))
        {
            return await WriteCurrentMarkerAsync(
                normalizedPath,
                cancellationToken);
        }

        return ValidateConfigured(normalizedPath);
    }

    public async Task<StorageValidationResult> RepairAsync(
        string? storagePath,
        CancellationToken cancellationToken = default)
    {
        StorageValidationResult pathResult =
            ValidateExistingDirectory(storagePath);

        if (!pathResult.IsValid)
        {
            return pathResult;
        }

        string normalizedPath =
            pathResult.NormalizedPath!;

        StorageValidationResult markerResult =
            ValidateMarker(normalizedPath);

        if (markerResult.IsValid)
        {
            return markerResult;
        }

        if (markerResult.FailureKind is
            StorageValidationFailureKind.STORAGE_VERSION_IS_NEWER or
            StorageValidationFailureKind.STORAGE_VERSION_UNSUPPORTED)
        {
            return markerResult;
        }

        if (!IsRepairableMarkerFailure(
                markerResult.FailureKind))
        {
            return markerResult;
        }

        string markerPath = Path.Combine(
            normalizedPath,
            STORAGE_MARKER_FILE_NAME);

        if (File.Exists(markerPath))
        {
            StorageValidationResult backupResult =
                BackupDamagedMarker(markerPath);

            if (!backupResult.IsValid)
            {
                return backupResult;
            }
        }

        return await WriteCurrentMarkerAsync(
            normalizedPath,
            cancellationToken);
    }

    private static StorageValidationResult ValidateExistingDirectory(
        string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return StorageValidationResult.Failure(
                "Папка хранилища не выбрана.");
        }

        string normalizedPath;

        try
        {
            normalizedPath = Path.GetFullPath(
                storagePath.Trim());
        }
        catch (Exception exception)
            when (exception is ArgumentException
                      or NotSupportedException
                      or PathTooLongException
                      or SecurityException)
        {
            return StorageValidationResult.Failure(
                "Указан недопустимый путь к хранилищу.");
        }

        if (!Directory.Exists(normalizedPath))
        {
            return StorageValidationResult.Failure(
                "Папка хранилища не найдена.");
        }

        return StorageValidationResult.Success(
            normalizedPath);
    }

    private static StorageValidationResult ValidateMarker(
        string storagePath)
    {
        string markerPath = Path.Combine(
            storagePath,
            STORAGE_MARKER_FILE_NAME);

        if (!File.Exists(markerPath))
        {
            return StorageValidationResult.Failure(
                "Служебные настройки хранилища отсутствуют.",
                StorageValidationFailureKind.MARKER_MISSING);
        }

        try
        {
            using FileStream stream =
                File.OpenRead(markerPath);

            using JsonDocument document =
                JsonDocument.Parse(stream);

            JsonElement root =
                document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty(
                    "formatVersion",
                    out JsonElement versionElement) ||
                versionElement.ValueKind !=
                JsonValueKind.Number ||
                !versionElement.TryGetInt32(
                    out int formatVersion))
            {
                return CorruptedMarkerFailure();
            }

            if (formatVersion ==
                CURRENT_STORAGE_FORMAT_VERSION)
            {
                return StorageValidationResult.Success(
                    storagePath);
            }

            return formatVersion >
                   CURRENT_STORAGE_FORMAT_VERSION
                ? StorageValidationResult.Failure(
                    "Хранилище создано в более новой версии Mnemora. Обновите приложение.",
                    StorageValidationFailureKind.STORAGE_VERSION_IS_NEWER)
                : StorageValidationResult.Failure(
                    "Версия хранилища не поддерживается этой версией Mnemora.",
                    StorageValidationFailureKind.STORAGE_VERSION_UNSUPPORTED);
        }
        catch (JsonException)
        {
            return CorruptedMarkerFailure();
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException
                      or NotSupportedException)
        {
            return StorageValidationResult.Failure(
                "Не удалось прочитать служебные настройки хранилища.");
        }
    }

    private static StorageValidationResult CorruptedMarkerFailure() =>
        StorageValidationResult.Failure(
            "Не удалось проверить хранилище. Служебные настройки повреждены.",
            StorageValidationFailureKind.MARKER_CORRUPTED);

    private static bool IsMarkerFailure(
        StorageValidationFailureKind failureKind) =>
        failureKind is
            StorageValidationFailureKind.MARKER_MISSING or
            StorageValidationFailureKind.MARKER_CORRUPTED or
            StorageValidationFailureKind.STORAGE_VERSION_IS_NEWER or
            StorageValidationFailureKind.STORAGE_VERSION_UNSUPPORTED;

    private static bool IsRepairableMarkerFailure(
        StorageValidationFailureKind failureKind) =>
        failureKind is
            StorageValidationFailureKind.MARKER_MISSING or
            StorageValidationFailureKind.MARKER_CORRUPTED;

    private static bool ContainsNoMnemoraData(
        string storagePath)
    {
        try
        {
            return Directory
                .EnumerateFileSystemEntries(
                    storagePath)
                .All(entry =>
                {
                    string name =
                        Path.GetFileName(entry);

                    return string.Equals(
                               name,
                               STORAGE_MARKER_FILE_NAME,
                               StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(
                               name,
                               OBSIDIAN_METADATA_DIRECTORY_NAME,
                               StringComparison.OrdinalIgnoreCase);
                });
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException
                      or NotSupportedException)
        {
            return false;
        }
    }

    private StorageValidationResult BackupDamagedMarker(
        string markerPath)
    {
        try
        {
            string recoveryPath = Path.Combine(
                localPathProvider.RootPath,
                RECOVERY_DIRECTORY_NAME);

            Directory.CreateDirectory(recoveryPath);

            string backupPath = Path.Combine(
                recoveryPath,
                $"storage-marker-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.bak");

            File.Copy(
                markerPath,
                backupPath,
                overwrite: false);

            return StorageValidationResult.Success(
                Path.GetDirectoryName(markerPath)!);
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException
                      or NotSupportedException)
        {
            return StorageValidationResult.Failure(
                "Не удалось подготовить восстановление хранилища.");
        }
    }

    private async Task<StorageValidationResult>
        WriteCurrentMarkerAsync(
            string storagePath,
            CancellationToken cancellationToken)
    {
        string markerPath = Path.Combine(
            storagePath,
            STORAGE_MARKER_FILE_NAME);

        string temporaryPath = Path.Combine(
            storagePath,
            $".mnemora-repair-{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                STORAGE_MARKER_CONTENT,
                cancellationToken);

            File.Move(
                temporaryPath,
                markerPath,
                overwrite: true);

            return ValidateConfigured(storagePath);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException
                      or NotSupportedException)
        {
            return StorageValidationResult.Failure(
                "Не удалось восстановить служебные настройки хранилища.");
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception)
                when (exception is IOException
                          or UnauthorizedAccessException
                          or SecurityException
                          or NotSupportedException)
            {
                // Временный файл будет удалён при следующем запуске.
            }
        }
    }

    private static bool CanWriteToDirectory(
        string path)
    {
        string testFilePath = Path.Combine(
            path,
            $".mnemora-write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            using FileStream stream = new(
                testFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.DeleteOnClose);

            stream.WriteByte(0);
            stream.Flush(
                flushToDisk: true);

            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException
                      or NotSupportedException)
        {
            return false;
        }
    }
}
