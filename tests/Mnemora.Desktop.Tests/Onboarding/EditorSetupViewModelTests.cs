using System.IO;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Startup;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Onboarding;
using Xunit;

namespace Mnemora.Desktop.Tests.Onboarding;

public sealed class EditorSetupViewModelTests : IDisposable
{
    private readonly string _storagePath = Path.Combine(
        Path.GetTempPath(),
        $"mnemora-editor-setup-{Guid.NewGuid():N}");

    [Fact]
    public void SelectObsidian_WhenStorageIsNotYetVault_EnablesCheckCommand()
    {
        Directory.CreateDirectory(_storagePath);

        var state = new OnboardingState
        {
            StoragePath = _storagePath,
            MarkdownEditor = MarkdownEditorType.VisualStudioCode,
        };

        var viewModel = new EditorSetupViewModel(
            new FakeFolderPickerService(),
            new FakeMarkdownEditorService(),
            new FakeSettingsService(),
            new FakeStorageTemporaryFilesCleanupService(),
            new FakeNavigationService(),
            state);

        viewModel.SelectObsidianCommand.Execute(null);

        Assert.True(viewModel.IsObsidianSelected);
        Assert.False(Directory.Exists(
            Path.Combine(_storagePath, ".obsidian")));
        Assert.True(
            viewModel.CheckConfigurationCommand.CanExecute(null));
    }

    [Fact]
    public async Task ContinueAsync_AfterObsidianVerification_CleansStorageTemporaryFiles()
    {
        Directory.CreateDirectory(
            Path.Combine(
                _storagePath,
                ".obsidian"));

        var state = new OnboardingState
        {
            StoragePath = _storagePath,
            MarkdownEditor = MarkdownEditorType.Obsidian,
            ObsidianVaultPath = _storagePath,
            IsObsidianVerified = true,
        };

        var cleanupService =
            new FakeStorageTemporaryFilesCleanupService();

        var viewModel = new EditorSetupViewModel(
            new FakeFolderPickerService(),
            new FakeMarkdownEditorService(),
            new FakeSettingsService(),
            cleanupService,
            new FakeNavigationService(),
            state);

        await viewModel.ContinueCommand.ExecuteAsync(null);

        Assert.Equal(
            _storagePath,
            cleanupService.CleanedStoragePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, recursive: true);
        }
    }

    private sealed class FakeFolderPickerService : IFolderPickerService
    {
        public string? SelectFolder(string? initialDirectory = null) =>
            null;
    }

    private sealed class FakeMarkdownEditorService : IMarkdownEditorService
    {
        public string? FindVisualStudioCodeExecutable() =>
            null;

        public bool IsObsidianInstalled() =>
            true;

        public MarkdownEditorLaunchResult OpenDownloadPage(
            MarkdownEditorType editor) =>
            new(true, string.Empty);

        public MarkdownEditorConfigurationValidationResult
            ValidateConfiguration(
                MarkdownEditorType? editor,
                string? visualStudioCodePath,
                string? obsidianVaultPath) =>
            new(true, string.Empty);

        public Task<MarkdownEditorLaunchResult> CheckAsync(
            MarkdownEditorType editor,
            string? visualStudioCodePath,
            string? obsidianVaultPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new MarkdownEditorLaunchResult(
                    true,
                    string.Empty));

        public Task<MarkdownEditorLaunchResult> OpenAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new MarkdownEditorLaunchResult(
                    true,
                    string.Empty));
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public ViewModelBase? CurrentViewModel =>
            null;

        private EventHandler? _currentViewModelChanged;

        public event EventHandler? CurrentViewModelChanged
        {
            add => _currentViewModelChanged += value;
            remove => _currentViewModelChanged -= value;
        }

        public void NavigateTo<TViewModel>()
            where TViewModel : ViewModelBase
        {
        }
    }

    private sealed class FakeStorageTemporaryFilesCleanupService
        : IStorageTemporaryFilesCleanupService
    {
        public string? CleanedStoragePath { get; private set; }

        public Task<StorageTemporaryFilesCleanupReport> CleanupAsync(
            string? storagePath,
            CancellationToken cancellationToken = default)
        {
            CleanedStoragePath = storagePath;

            return Task.FromResult(
                new StorageTemporaryFilesCleanupReport(0, 0));
        }
    }

    private sealed class FakeSettingsService : ISettingsService
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
