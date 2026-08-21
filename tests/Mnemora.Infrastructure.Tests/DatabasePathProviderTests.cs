using Microsoft.Data.Sqlite;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class DatabasePathProviderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/path")]
    public void GetDatabasePath_RejectsMissingOrRelativeStoragePath(string? storagePath)
    {
        var result = DatabasePathProvider.GetDatabasePath(storagePath);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void GetDatabasePath_ReturnsDatabaseInsideHiddenSystemDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();

        var result = DatabasePathProvider.GetDatabasePath(temporaryDirectory.Path);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            System.IO.Path.Combine(
                temporaryDirectory.Path,
                ".mnemora-data",
                "mnemora.db"),
            result.Value);
    }

    [Fact]
    public void CreateConnectionString_CreatesDirectoryAndEnablesForeignKeys()
    {
        using var temporaryDirectory = new TemporaryDirectory();

        var result = DatabasePathProvider.CreateConnectionString(temporaryDirectory.Path);

        Assert.True(result.IsSuccess);
        var builder = new SqliteConnectionStringBuilder(result.Value);
        Assert.Equal(SqliteOpenMode.ReadWriteCreate, builder.Mode);
        Assert.True(builder.ForeignKeys);
        Assert.True(Directory.Exists(
            System.IO.Path.Combine(temporaryDirectory.Path, ".mnemora-data")));
    }

    [Fact]
    public void GetDatabasePath_RejectsPathPointingToFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        string filePath = System.IO.Path.Combine(temporaryDirectory.Path, "storage.txt");
        File.WriteAllText(filePath, "not a directory");

        var result = DatabasePathProvider.GetDatabasePath(filePath);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.path.points.to.file", result.Error.Code);
    }
}
