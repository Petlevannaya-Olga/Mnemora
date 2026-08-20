using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Desktop.Startup;
using Xunit;

namespace Mnemora.Desktop.Tests.Startup;

public sealed class LocalAppDataCleanupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Mnemora.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CleanupAsync_DeletesOnlyTempAndStagingContents()
    {
        var paths = new TestPathProvider(_root);
        Directory.CreateDirectory(paths.TempPath);
        Directory.CreateDirectory(paths.StagingPath);
        Directory.CreateDirectory(Path.Combine(paths.RootPath, "Keep"));
        Directory.CreateDirectory(Path.Combine(paths.StagingPath, "nested"));
        await File.WriteAllTextAsync(Path.Combine(paths.TempPath, "temp.txt"), "temp");
        await File.WriteAllTextAsync(Path.Combine(paths.StagingPath, "nested", "stage.txt"), "stage");
        string keepPath = Path.Combine(paths.RootPath, "Keep", "keep.txt");
        await File.WriteAllTextAsync(keepPath, "keep");

        var service = new LocalAppDataCleanupService(paths, NullLogger<LocalAppDataCleanupService>.Instance);
        LocalAppDataCleanupReport report = await service.CleanupAsync();

        Assert.Empty(Directory.GetFileSystemEntries(paths.TempPath));
        Assert.Empty(Directory.GetFileSystemEntries(paths.StagingPath));
        Assert.True(File.Exists(keepPath));
        Assert.Equal(2, report.DeletedCount);
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
        public string StagingPath => Path.Combine(RootPath, "Staging");
    }
}
