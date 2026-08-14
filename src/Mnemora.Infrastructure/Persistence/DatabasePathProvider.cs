using Microsoft.Data.Sqlite;

namespace Mnemora.Infrastructure.Persistence;

public static class DatabasePathProvider
{
    private const string APPLICATION_DIRECTORY = "Mnemora";

    private const string DATA_DIRECTORY = "Data";

    private const string DATABASE_FILE_NAME = "mnemora.db";

    public static string GetDatabasePath()
    {
        string localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationDataPath))
        {
            throw new InvalidOperationException("Не удалось определить папку локальных данных приложения.");
        }

        return Path.Combine(
            localApplicationDataPath,
            APPLICATION_DIRECTORY,
            DATA_DIRECTORY,
            DATABASE_FILE_NAME);
    }

    public static string CreateConnectionString()
    {
        string databasePath = GetDatabasePath();

        string? databaseDirectory = Path.GetDirectoryName(databasePath);

        if (string.IsNullOrWhiteSpace(databaseDirectory))
        {
            throw new InvalidOperationException("Не удалось определить папку базы данных.");
        }

        Directory.CreateDirectory(databaseDirectory);

        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }
}