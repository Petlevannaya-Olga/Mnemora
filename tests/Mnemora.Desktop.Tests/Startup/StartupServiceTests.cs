using System.IO;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Startup;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Desktop.Tests.Startup;

public sealed class StartupServiceTests : IDisposable
{
    private readonly string _storagePath = Path.Combine(
        Path.GetTempPath(),
        "Mnemora.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeAsync_WithoutCompletedOnboarding_SkipsDatabaseAndReportsAllStages()
    {
        var database = new RecordingDatabaseInitializer();
        var cleanup = new StubCleanupService(new LocalAppDataCleanupReport(2, 0));
        var progress = new RecordingProgress();
        var service = CreateService(new AppSettings(), database, cleanup);

        StartupResult result = await service.InitializeAsync(progress);

        Assert.True(result.IsSuccess);
        Assert.False(result.WasOnboardingCompleted);
        Assert.False(result.StorageIsConfigured);
        Assert.False(result.EditorIsConfigured);
        Assert.Equal(0, database.CallCount);
        Assert.Equal(
            new[] { 5, 20, 40, 90, 100 },
            progress.Values.Select(value => value.Percent));
    }

    [Fact]
    public async Task InitializeAsync_WithConfiguredStorage_InitializesDatabase()
    {
        Directory.CreateDirectory(_storagePath);
        var settings = new AppSettings
        {
            StoragePath = _storagePath,
            MarkdownEditor = MarkdownEditorType.VisualStudioCode,
            VisualStudioCodePath = "code.exe",
            IsOnboardingCompleted = true,
        };
        var database = new RecordingDatabaseInitializer();
        var progress = new RecordingProgress();
        var service = CreateService(
            settings,
            database,
            new StubCleanupService(new LocalAppDataCleanupReport(0, 0)));

        StartupResult result = await service.InitializeAsync(progress);

        Assert.True(result.IsSuccess);
        Assert.True(result.WasOnboardingCompleted);
        Assert.True(result.StorageIsConfigured);
        Assert.True(result.EditorIsConfigured);
        Assert.Equal(1, database.CallCount);
        Assert.Equal(100, progress.Values.Last().Percent);
    }

    [Fact]
    public async Task InitializeAsync_WhenConfiguredStorageIsMissing_ReturnsFailureBeforeDatabase()
    {
        var settings = new AppSettings
        {
            StoragePath = _storagePath,
            MarkdownEditor = MarkdownEditorType.VisualStudioCode,
            VisualStudioCodePath = "code.exe",
            IsOnboardingCompleted = true,
        };
        var database = new RecordingDatabaseInitializer();
        var progress = new RecordingProgress();
        var service = CreateService(
            settings,
            database,
            new StubCleanupService(new LocalAppDataCleanupReport(0, 0)));

        StartupResult result = await service.InitializeAsync(progress);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Папка хранилища не найдена", result.ErrorMessage!);
        Assert.Equal(0, database.CallCount);
        Assert.Equal(
            new[] { 5, 20, 40 },
            progress.Values.Select(value => value.Percent));
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, recursive: true);
        }
    }

    private static StartupService CreateService(
        AppSettings settings,
        RecordingDatabaseInitializer database,
        ILocalAppDataCleanupService cleanup) =>
        new(
            new StubSettingsService(settings),
            new OnboardingState(),
            database,
            cleanup,
            NullLogger<StartupService>.Instance);

    private sealed class RecordingProgress : IProgress<StartupProgress>
    {
        public List<StartupProgress> Values { get; } = [];

        public void Report(StartupProgress value) => Values.Add(value);
    }

    private sealed class RecordingDatabaseInitializer : IDatabaseInitializer
    {
        public int CallCount { get; private set; }

        public Task<UnitResult<Error>> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(UnitResult.Success<Error>());
        }
    }

    private sealed class StubCleanupService(LocalAppDataCleanupReport report)
        : ILocalAppDataCleanupService
    {
        public Task<LocalAppDataCleanupReport> CleanupAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(report);
    }

    private sealed class StubSettingsService(AppSettings settings)
        : ISettingsService
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveStoragePathAsync(string storagePath, CancellationToken cancellationToken = default) =>
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
