using System.IO;
using CSharpFunctionalExtensions;
using Mnemora.Application.Database;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Security;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Shell;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Desktop.Tests.Onboarding;

public sealed class CompletionSetupViewModelTests
    : IDisposable
{
    private readonly string _storagePath = Path.Combine(
        Path.GetTempPath(),
        "Mnemora.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task OpenMnemoraAsync_WhenStorageWasDeleted_DoesNotCompleteOnboarding()
    {
        await CreateValidStorageAsync();
        Directory.Delete(
            _storagePath,
            recursive: true);

        TestContext context =
            CreateContext();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.Contains(
            "не найдена",
            context.ViewModel.CompletionErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, context.Database.CallCount);
        Assert.Equal(0, context.Settings.CompleteCallCount);
        Assert.False(context.State.IsOnboardingCompleted);
        Assert.Null(context.Navigation.LastViewModelType);
    }

    [Fact]
    public async Task OpenMnemoraAsync_WhenStorageMarkerWasDeleted_DoesNotCompleteOnboarding()
    {
        await CreateValidStorageAsync();
        File.Delete(
            Path.Combine(
                _storagePath,
                ".mnemora"));

        TestContext context =
            CreateContext();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.Contains(
            ".mnemora",
            context.ViewModel.CompletionErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, context.Database.CallCount);
        Assert.Equal(0, context.Settings.CompleteCallCount);
    }

    [Fact]
    public async Task OpenMnemoraAsync_WhenEditorConfigurationWasDeleted_RequiresNewVerification()
    {
        await CreateValidStorageAsync();

        TestContext context =
            CreateContext(
                editorValidationResult:
                    new MarkdownEditorConfigurationValidationResult(
                        false,
                        "Редактор больше не найден."));

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.Equal(
            "Редактор больше не найден.",
            context.ViewModel.CompletionErrorMessage);
        Assert.False(
            context.State.IsMarkdownEditorVerified);
        Assert.Equal(0, context.Database.CallCount);
        Assert.Equal(0, context.Settings.CompleteCallCount);
    }

    [Fact]
    public async Task OpenMnemoraAsync_WhenStorageIsDeletedDuringDatabaseInitialization_DoesNotCompleteOnboarding()
    {
        await CreateValidStorageAsync();

        TestContext context =
            CreateContext(
                onDatabaseInitialize: () =>
                    Directory.Delete(
                        _storagePath,
                        recursive: true));

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.Equal(1, context.Database.CallCount);
        Assert.Equal(0, context.Settings.CompleteCallCount);
        Assert.False(context.State.IsOnboardingCompleted);
        Assert.Contains(
            "не найдена",
            context.ViewModel.CompletionErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenMnemoraAsync_WithValidConfiguration_CompletesOnboarding()
    {
        await CreateValidStorageAsync();

        TestContext context =
            CreateContext();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.Null(
            context.ViewModel.CompletionErrorMessage);
        Assert.Equal(1, context.Database.CallCount);
        Assert.Equal(1, context.Settings.CompleteCallCount);
        Assert.True(context.State.IsOnboardingCompleted);
        Assert.Equal(
            typeof(AppShellViewModel),
            context.Navigation.LastViewModelType);
    }

    private async Task CreateValidStorageAsync()
    {
        Directory.CreateDirectory(_storagePath);
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                ".mnemora"),
            "{\"formatVersion\":1}");
    }

    private TestContext CreateContext(
        MarkdownEditorConfigurationValidationResult?
            editorValidationResult = null,
        Action? onDatabaseInitialize = null)
    {
        var state = new OnboardingState
        {
            UserName = "Ольга",
            StoragePath = _storagePath,
            MarkdownEditor =
                MarkdownEditorType.VisualStudioCode,
            VisualStudioCodePath = "Code.exe",
            IsVisualStudioCodeVerified = true,
        };

        var navigation =
            new RecordingNavigationService();

        var settings =
            new RecordingSettingsService();

        var database =
            new RecordingDatabaseInitializer(
                onDatabaseInitialize);

        var viewModel =
            new CompletionSetupViewModel(
                navigation,
                state,
                new StubApiKeyStore(),
                settings,
                database,
                new StorageValidationService(),
                new StubMarkdownEditorService(
                    editorValidationResult ??
                    new MarkdownEditorConfigurationValidationResult(
                        true,
                        string.Empty)));

        return new TestContext(
            viewModel,
            state,
            navigation,
            settings,
            database);
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

    private sealed record TestContext(
        CompletionSetupViewModel ViewModel,
        OnboardingState State,
        RecordingNavigationService Navigation,
        RecordingSettingsService Settings,
        RecordingDatabaseInitializer Database);

    private sealed class RecordingNavigationService
        : INavigationService
    {
        public Type? LastViewModelType { get; private set; }

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
            LastViewModelType = typeof(TViewModel);
            _currentViewModelChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingDatabaseInitializer(
        Action? onInitialize)
        : IDatabaseInitializer
    {
        public int CallCount { get; private set; }

        public Task<UnitResult<Error>> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            onInitialize?.Invoke();

            return Task.FromResult(
                UnitResult.Success<Error>());
        }
    }

    private sealed class StubMarkdownEditorService(
        MarkdownEditorConfigurationValidationResult validationResult)
        : IMarkdownEditorService
    {
        public string? FindVisualStudioCodeExecutable() =>
            null;

        public bool IsObsidianInstalled() =>
            false;

        public MarkdownEditorLaunchResult OpenDownloadPage(
            MarkdownEditorType editor) =>
            new(true, string.Empty);

        public MarkdownEditorConfigurationValidationResult
            ValidateConfiguration(
                MarkdownEditorType? editor,
                string? visualStudioCodePath,
                string? obsidianVaultPath) =>
            validationResult;

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

    private sealed class StubApiKeyStore
        : IApiKeyStore
    {
        public string? Load() =>
            null;

        public void Save(string apiKey)
        {
        }

        public void Delete()
        {
        }
    }

    private sealed class RecordingSettingsService
        : ISettingsService
    {
        public int CompleteCallCount { get; private set; }

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
            CancellationToken cancellationToken = default)
        {
            CompleteCallCount++;
            return Task.CompletedTask;
        }
    }
}
