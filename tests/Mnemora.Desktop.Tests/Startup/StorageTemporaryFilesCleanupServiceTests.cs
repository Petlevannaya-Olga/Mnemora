using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Desktop.Startup;
using Xunit;

namespace Mnemora.Desktop.Tests.Startup;

public sealed class StorageTemporaryFilesCleanupServiceTests
    : IDisposable
{
    private readonly string _storagePath = Path.Combine(
        Path.GetTempPath(),
        "Mnemora.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CleanupAsync_DeletesOnlyDraftTemporaryDirectory()
    {
        string draftsDirectory = Path.Combine(
            _storagePath,
            "materials",
            "_drafts");

        // Статус Draft хранится в БД; на диске это такой же полноценный
        // материал в articles, а не временный файл в _drafts.
        string draftStatusArticleDirectory = Path.Combine(
            _storagePath,
            "materials",
            "articles",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            Path.Combine(
                draftsDirectory,
                "editor-check"));

        Directory.CreateDirectory(
            draftStatusArticleDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(
                draftsDirectory,
                "editor-check",
                "mnemora-editor-check.md"),
            "temporary");

        string materialPath = Path.Combine(
            draftStatusArticleDirectory,
            "content.md");

        await File.WriteAllTextAsync(
            materialPath,
            "material");

        var service =
            new StorageTemporaryFilesCleanupService(
                NullLogger<StorageTemporaryFilesCleanupService>.Instance);

        StorageTemporaryFilesCleanupReport report =
            await service.CleanupAsync(
                _storagePath);

        Assert.False(
            Directory.Exists(draftsDirectory));
        Assert.True(
            File.Exists(materialPath));
        Assert.Equal(1, report.DeletedCount);
        Assert.Equal(0, report.SkippedCount);
    }

    [Fact]
    public async Task CleanupAsync_WhenMaterialsContainsOnlyDrafts_DeletesEmptyMaterialsDirectory()
    {
        string materialsDirectory = Path.Combine(
            _storagePath,
            "materials");

        string editorCheckDirectory = Path.Combine(
            materialsDirectory,
            "_drafts",
            "editor-check");

        Directory.CreateDirectory(
            editorCheckDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(
                editorCheckDirectory,
                "mnemora-editor-check.md"),
            "temporary");

        var service =
            new StorageTemporaryFilesCleanupService(
                NullLogger<StorageTemporaryFilesCleanupService>.Instance);

        StorageTemporaryFilesCleanupReport report =
            await service.CleanupAsync(
                _storagePath);

        Assert.False(
            Directory.Exists(materialsDirectory));
        Assert.Equal(1, report.DeletedCount);
        Assert.Equal(0, report.SkippedCount);
    }

    [Fact]
    public async Task CleanupAsync_WhenMaterialsContainsAnotherDirectory_KeepsMaterialsDirectory()
    {
        string materialsDirectory = Path.Combine(
            _storagePath,
            "materials");

        string editorCheckDirectory = Path.Combine(
            materialsDirectory,
            "_drafts",
            "editor-check");

        string userDirectory = Path.Combine(
            materialsDirectory,
            "articles");

        Directory.CreateDirectory(
            editorCheckDirectory);

        Directory.CreateDirectory(
            userDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(
                editorCheckDirectory,
                "mnemora-editor-check.md"),
            "temporary");

        var service =
            new StorageTemporaryFilesCleanupService(
                NullLogger<StorageTemporaryFilesCleanupService>.Instance);

        StorageTemporaryFilesCleanupReport report =
            await service.CleanupAsync(
                _storagePath);

        Assert.True(
            Directory.Exists(materialsDirectory));
        Assert.True(
            Directory.Exists(userDirectory));
        Assert.False(
            Directory.Exists(
                Path.Combine(
                    materialsDirectory,
                    "_drafts")));
        Assert.Equal(1, report.DeletedCount);
        Assert.Equal(0, report.SkippedCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CleanupAsync_WithoutStoragePath_ReturnsEmptyReport(
        string? storagePath)
    {
        var service =
            new StorageTemporaryFilesCleanupService(
                NullLogger<StorageTemporaryFilesCleanupService>.Instance);

        StorageTemporaryFilesCleanupReport report =
            await service.CleanupAsync(storagePath);

        Assert.Equal(0, report.DeletedCount);
        Assert.Equal(0, report.SkippedCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(
                _storagePath,
                recursive: true);
        }
    }
}
