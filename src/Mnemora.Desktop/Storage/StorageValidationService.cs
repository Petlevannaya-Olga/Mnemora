using System.IO;
using System.Security;
using System.Text.Json;

namespace Mnemora.Desktop.Storage;

public sealed class StorageValidationService
    : IStorageValidationService
{
    private const string StorageMarkerFileName =
        ".mnemora";

    private const int CurrentStorageFormatVersion = 1;

    private const string StorageMarkerContent =
        "{\"formatVersion\":1}";

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
        StorageValidationResult candidateResult =
            ValidateCandidate(storagePath);

        if (!candidateResult.IsValid)
        {
            return candidateResult;
        }

        string normalizedPath =
            candidateResult.NormalizedPath!;

        string markerPath = Path.Combine(
            normalizedPath,
            StorageMarkerFileName);

        if (!File.Exists(markerPath))
        {
            try
            {
                await File.WriteAllTextAsync(
                    markerPath,
                    StorageMarkerContent,
                    cancellationToken);
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
                    "Не удалось создать файл маркера хранилища Mnemora.");
            }
        }

        return ValidateConfigured(normalizedPath);
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
            StorageMarkerFileName);

        if (!File.Exists(markerPath))
        {
            return StorageValidationResult.Failure(
                "Не найден файл .mnemora. Папка должна быть пустой или являться хранилищем Mnemora.");
        }

        try
        {
            using FileStream stream =
                File.OpenRead(markerPath);

            using JsonDocument document =
                JsonDocument.Parse(stream);

            JsonElement root =
                document.RootElement;

            bool isValid =
                root.ValueKind == JsonValueKind.Object
                &&
                root.TryGetProperty(
                    "formatVersion",
                    out JsonElement versionElement)
                &&
                versionElement.ValueKind ==
                JsonValueKind.Number
                &&
                versionElement.TryGetInt32(
                    out int formatVersion)
                &&
                formatVersion ==
                CurrentStorageFormatVersion;

            return isValid
                ? StorageValidationResult.Success(
                    storagePath)
                : StorageValidationResult.Failure(
                    "Файл .mnemora повреждён или имеет неподдерживаемую версию.");
        }
        catch (JsonException)
        {
            return StorageValidationResult.Failure(
                "Файл .mnemora повреждён или имеет неподдерживаемую версию.");
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or SecurityException
                      or NotSupportedException)
        {
            return StorageValidationResult.Failure(
                "Не удалось прочитать файл .mnemora.");
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
