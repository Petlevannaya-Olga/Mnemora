using System.IO;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Settings;
using Xunit;

namespace Mnemora.Desktop.Tests.Editors;

public sealed class MarkdownEditorServiceTests
    : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "Mnemora.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ValidateConfiguration_WhenVisualStudioCodeExecutableWasDeleted_ReturnsFailure()
    {
        Directory.CreateDirectory(_testDirectory);

        string executablePath = Path.Combine(
            _testDirectory,
            "Code.exe");

        await File.WriteAllTextAsync(
            executablePath,
            string.Empty);

        var service = new MarkdownEditorService(
            new StubSettingsService());

        MarkdownEditorConfigurationValidationResult beforeDeletion =
            service.ValidateConfiguration(
                MarkdownEditorType.VisualStudioCode,
                executablePath,
                null);

        File.Delete(executablePath);

        MarkdownEditorConfigurationValidationResult afterDeletion =
            service.ValidateConfiguration(
                MarkdownEditorType.VisualStudioCode,
                executablePath,
                null);

        Assert.True(beforeDeletion.IsValid);
        Assert.False(afterDeletion.IsValid);
        Assert.Contains(
            "Code.exe",
            afterDeletion.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateConfiguration_WhenObsidianMetadataDirectoryWasDeleted_ReturnsFailure()
    {
        Directory.CreateDirectory(_testDirectory);

        var service = new MarkdownEditorService(
            new StubSettingsService());

        MarkdownEditorConfigurationValidationResult result =
            service.ValidateConfiguration(
                MarkdownEditorType.Obsidian,
                null,
                _testDirectory);

        Assert.False(result.IsValid);
        Assert.Contains(
            "Vault Obsidian",
            result.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(
                _testDirectory,
                recursive: true);
        }
    }

    private sealed class StubSettingsService
        : ISettingsService
    {
        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppSettings());

        public Task SaveUserNameAsync(
            string userName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveStoragePathAsync(
            string storagePath,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveMarkdownEditorAsync(
            MarkdownEditorType? editor,
            string? visualStudioCodePath,
            string? obsidianVaultPath,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryOverviewViewModeAsync(
            LibraryOverviewViewMode viewMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryManagementViewModeAsync(
            LibraryManagementViewMode viewMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryManagementSectionsViewModeAsync(
            LibraryManagementViewMode viewMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryManagementTopicsViewModeAsync(
            LibraryManagementViewMode viewMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryManagementMaterialsViewModeAsync(
            LibraryManagementViewMode viewMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryTopicsViewModeAsync(
            LibraryTopicsViewMode viewMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryManagementSectionSortAsync(
            LibraryManagementSortMode sortMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryManagementTopicSortAsync(
            Guid sectionId,
            LibraryManagementSortMode sortMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLibraryManagementMaterialSortAsync(
            Guid topicId,
            LibraryManagementSortMode sortMode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CompleteOnboardingAsync(
            bool isAiConfigured,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
