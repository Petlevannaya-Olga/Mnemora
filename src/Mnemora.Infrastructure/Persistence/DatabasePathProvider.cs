using CSharpFunctionalExtensions;
using Microsoft.Data.Sqlite;
using Mnemora.Shared;
using System.Security;

namespace Mnemora.Infrastructure.Persistence;

public static class DatabasePathProvider
{
    private const string SystemDirectoryName = ".mnemora-data";
    private const string DatabaseFileName = "mnemora.db";

    public static Result<string, Error> GetDatabasePath(string? storagePath)
    {
        var normalizedStoragePathResult = NormalizeStoragePath(storagePath);

        if (normalizedStoragePathResult.IsFailure) return normalizedStoragePathResult.Error;

        return Path.Combine(normalizedStoragePathResult.Value, SystemDirectoryName, DatabaseFileName);
    }

    public static Result<string, Error> CreateConnectionString(string? storagePath)
    {
        var databasePathResult = GetDatabasePath(storagePath);

        if (databasePathResult.IsFailure) return databasePathResult.Error;

        var directoryResult = EnsureDatabaseDirectoryExists(databasePathResult.Value);

        if (directoryResult.IsFailure) return directoryResult.Error;

        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePathResult.Value,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    private static Result<string, Error> NormalizeStoragePath(string? storagePath)
    {
        if (storagePath is null) return CommonErrors.IsRequired(nameof(storagePath));

        var trimmedStoragePath = storagePath.Trim();

        if (trimmedStoragePath.Length == 0) return CommonErrors.IsEmpty(nameof(storagePath));

        if (!Path.IsPathFullyQualified(trimmedStoragePath))
        {
            return CommonErrors.Validation(
                "storage.path.must.be.absolute",
                "Путь к хранилищу должен быть абсолютным.",
                nameof(storagePath));
        }

        string normalizedStoragePath;

        try
        {
            normalizedStoragePath = Path.GetFullPath(trimmedStoragePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CommonErrors.Validation(
                "storage.path.is.invalid",
                "Указан недопустимый путь к хранилищу.",
                nameof(storagePath));
        }

        if (File.Exists(normalizedStoragePath))
        {
            return CommonErrors.Validation(
                "storage.path.points.to.file",
                "Путь к хранилищу указывает на файл.",
                nameof(storagePath));
        }

        return normalizedStoragePath;
    }

    private static UnitResult<Error> EnsureDatabaseDirectoryExists(string databasePath)
    {
        var databaseDirectory = Path.GetDirectoryName(databasePath);

        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            return CommonErrors.Failure(
                "database.directory.path.not.found",
                "Не удалось определить папку базы данных.");
        }

        try
        {
            Directory.CreateDirectory(databaseDirectory);
            return UnitResult.Success<Error>();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return CommonErrors.Failure(
                "database.directory.create.failed",
                "Не удалось создать системную папку базы данных.");
        }
    }
}