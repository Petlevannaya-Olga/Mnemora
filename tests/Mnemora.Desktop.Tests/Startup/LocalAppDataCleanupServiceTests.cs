using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Desktop.Startup;
using Xunit;

namespace Mnemora.Desktop.Tests.Startup;

public sealed class LocalAppDataCleanupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Mnemora.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CleanupAsync_DeletesTempDirectory_AndKeepsOtherRootContents()
    {
        var paths = new TestPathProvider(_root);

        Directory.CreateDirectory(paths.TempPath);

        string keepDirectoryPath = Path.Combine(paths.RootPath, "Keep");
        Directory.CreateDirectory(keepDirectoryPath);

        await File.WriteAllTextAsync(
            Path.Combine(paths.TempPath, "temp.txt"),
            "temp");

        string keepPath = Path.Combine(keepDirectoryPath, "keep.txt");
        await File.WriteAllTextAsync(keepPath, "keep");

        var service = new LocalAppDataCleanupService(
            paths,
            NullLogger<LocalAppDataCleanupService>.Instance);

        LocalAppDataCleanupReport report = await service.CleanupAsync();

        Assert.False(Directory.Exists(paths.TempPath));
        Assert.True(File.Exists(keepPath));
        Assert.Equal(1, report.DeletedCount);
        Assert.Equal(0, report.SkippedCount);
    }

    [Fact]
    public async Task CleanupAsync_WhenDirectoriesDoNotExist_ReturnsEmptyReport()
    {
        var paths = new TestPathProvider(_root);
        var service = new LocalAppDataCleanupService(paths, NullLogger<LocalAppDataCleanupService>.Instance);

        LocalAppDataCleanupReport report = await service.CleanupAsync();

        Assert.Equal(0, report.DeletedCount);
        Assert.Equal(0, report.SkippedCount);
    }

    [Fact]
    public async Task CleanupAsync_DeletesNestedTempContentsAndCountsTopLevelEntries()
    {
        var paths = new TestPathProvider(_root);
        string nestedDirectory = Path.Combine(paths.TempPath, "nested");
        Directory.CreateDirectory(nestedDirectory);
        await File.WriteAllTextAsync(Path.Combine(paths.TempPath, "top.txt"), "top");
        await File.WriteAllTextAsync(Path.Combine(nestedDirectory, "nested.txt"), "nested");

        var service = new LocalAppDataCleanupService(
            paths,
            NullLogger<LocalAppDataCleanupService>.Instance);

        LocalAppDataCleanupReport report = await service.CleanupAsync();

        Assert.False(Directory.Exists(paths.TempPath));
        Assert.Equal(2, report.DeletedCount);
        Assert.Equal(0, report.SkippedCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private sealed class TestPathProvider(string rootPath) : IMnemoraLocalPathProvider
    {
        public string RootPath { get; } = rootPath;
        public string TempPath => Path.Combine(RootPath, "Temp");
    }
}
