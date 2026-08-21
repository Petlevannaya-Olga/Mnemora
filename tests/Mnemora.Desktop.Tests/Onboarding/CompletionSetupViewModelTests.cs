using System.IO;
using CSharpFunctionalExtensions;
using Mnemora.Application.Database;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Security;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Startup;
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

    private readonly string _localRootPath = Path.Combine(
        Path.GetTempPath(),
        "Mnemora.Tests.Local",
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
        Assert.Equal(
            "Проверьте настройки",
            context.ViewModel.CompletionTitle);
        Assert.Equal(
            context.ViewModel.CompletionErrorMessage,
            context.ViewModel.CompletionSubtitle);
        Assert.True(context.ViewModel.HasStorageError);
        Assert.False(context.ViewModel.HasEditorError);
        Assert.Equal(
            "Изменить хранилище",
            context.ViewModel.BackButtonText);
        Assert.Equal(
            "Повторить",
            context.ViewModel.PrimaryActionText);
        Assert.Equal(0, context.Database.CallCount);
        Assert.Equal(0, context.Settings.CompleteCallCount);
        Assert.False(context.State.IsOnboardingCompleted);
        Assert.Null(context.Navigation.LastViewModelType);

        context.ViewModel.BackCommand.Execute(null);

        Assert.Equal(
            typeof(StorageSetupViewModel),
            context.Navigation.LastViewModelType);
    }

    [Fact]
    public async Task OpenMnemoraAsync_AfterStorageWasRestored_ClearsErrorAndCompletesOnboarding()
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

        Assert.True(context.ViewModel.HasStorageError);
        Assert.Equal(
            "Повторить",
            context.ViewModel.PrimaryActionText);

        await CreateValidStorageAsync();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.False(context.ViewModel.HasCompletionError);
        Assert.False(context.ViewModel.HasStorageError);
        Assert.Null(context.ViewModel.CompletionErrorMessage);
        Assert.Equal(
            "Открыть Mnemora",
            context.ViewModel.PrimaryActionText);
        Assert.Equal(1, context.Database.CallCount);
        Assert.Equal(1, context.Settings.CompleteCallCount);
        Assert.True(context.State.IsOnboardingCompleted);
        Assert.Equal(
            typeof(AppShellViewModel),
            context.Navigation.LastViewModelType);
    }

    [Fact]
    public async Task StorageStatus_ReturnsSelectedStoragePath()
    {
        await CreateValidStorageAsync();

        TestContext context =
            CreateContext();

        Assert.Equal(
            _storagePath,
            context.ViewModel.StorageStatus);
    }

    [Fact]
    public async Task OpenMnemoraAsync_WhenMarkerWasDeletedFromEmptyStorage_RecreatesMarkerAndCompletesOnboarding()
    {
        await CreateValidStorageAsync();
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        File.Delete(markerPath);

        TestContext context =
            CreateContext();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.True(File.Exists(markerPath));
        Assert.False(context.ViewModel.HasCompletionError);
        Assert.Equal(1, context.Database.CallCount);
        Assert.Equal(1, context.Settings.CompleteCallCount);
        Assert.True(context.State.IsOnboardingCompleted);
        Assert.Equal(
            typeof(AppShellViewModel),
            context.Navigation.LastViewModelType);
    }

    [Fact]
    public async Task OpenMnemoraAsync_WhenNonEmptyDirectoryHasNoMarker_OffersRecoveryWithoutAdoptingAutomatically()
    {
        await CreateValidStorageAsync();
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        File.Delete(markerPath);
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                "foreign.txt"),
            "foreign");

        TestContext context =
            CreateContext();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.False(File.Exists(markerPath));
        Assert.True(context.ViewModel.HasStorageError);
        Assert.True(context.ViewModel.CanRepairStorage);
        Assert.Equal(
            "Восстановить хранилище",
            context.ViewModel.PrimaryActionText);
        Assert.DoesNotContain(
            ".mnemora",
            context.ViewModel.CompletionErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, context.Database.CallCount);
        Assert.Equal(0, context.Settings.CompleteCallCount);
        Assert.False(context.State.IsOnboardingCompleted);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"formatVersion\":999}")]
    public async Task OpenMnemoraAsync_WhenEmptyStorageMarkerIsInvalid_RecreatesMarkerSilently(
        string markerContent)
    {
        Directory.CreateDirectory(_storagePath);
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        await File.WriteAllTextAsync(
            markerPath,
            markerContent);

        TestContext context =
            CreateContext();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.False(context.ViewModel.HasCompletionError);
        Assert.Equal(
            "{\"formatVersion\":1}",
            await File.ReadAllTextAsync(
                markerPath));
        Assert.Equal(1, context.Database.CallCount);
        Assert.Equal(1, context.Settings.CompleteCallCount);
        Assert.Equal(
            typeof(AppShellViewModel),
            context.Navigation.LastViewModelType);
    }

    [Fact]
    public async Task OpenMnemoraAsync_WhenNonEmptyStorageMarkerIsCorrupted_RepairsOnlyAfterConfirmation()
    {
        Directory.CreateDirectory(_storagePath);
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        await File.WriteAllTextAsync(
            markerPath,
            "not-json");
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                "material.md"),
            "material");

        TestContext context =
            CreateContext();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.True(context.ViewModel.CanRepairStorage);
        Assert.Equal(
            "Восстановить хранилище",
            context.ViewModel.PrimaryActionText);
        Assert.Equal(
            "not-json",
            await File.ReadAllTextAsync(
                markerPath));
        Assert.Equal(0, context.Database.CallCount);

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.False(context.ViewModel.HasCompletionError);
        Assert.Equal(
            "{\"formatVersion\":1}",
            await File.ReadAllTextAsync(
                markerPath));
        Assert.Equal(1, context.Database.CallCount);
        Assert.Equal(1, context.Settings.CompleteCallCount);
        Assert.Equal(
            typeof(AppShellViewModel),
            context.Navigation.LastViewModelType);
    }

    [Fact]
    public async Task OpenMnemoraAsync_WhenNonEmptyStorageVersionIsNewer_DoesNotOfferDowngrade()
    {
        Directory.CreateDirectory(_storagePath);
        string markerPath = Path.Combine(
            _storagePath,
            ".mnemora");

        const string markerContent =
            "{\"formatVersion\":999}";

        await File.WriteAllTextAsync(
            markerPath,
            markerContent);
        await File.WriteAllTextAsync(
            Path.Combine(
                _storagePath,
                "material.md"),
            "material");

        TestContext context =
            CreateContext();

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.True(context.ViewModel.HasStorageError);
        Assert.False(context.ViewModel.CanRepairStorage);
        Assert.Equal(
            "Повторить",
            context.ViewModel.PrimaryActionText);
        Assert.Contains(
            "более новой версии",
            context.ViewModel.CompletionErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            markerContent,
            await File.ReadAllTextAsync(
                markerPath));
        Assert.Equal(0, context.Database.CallCount);
        Assert.Equal(0, context.Settings.CompleteCallCount);
    }

    [Theory]
    [InlineData(
        MarkdownEditorType.VisualStudioCode,
        "Visual Studio Code не найден. Проверьте путь к Code.exe.")]
    [InlineData(
        MarkdownEditorType.Obsidian,
        "Obsidian не найден. Установите приложение и повторите проверку.")]
    public async Task OpenMnemoraAsync_WhenEditorApplicationWasUninstalled_RequiresEditorSetup(
        MarkdownEditorType editor,
        string expectedMessage)
    {
        await CreateValidStorageAsync();

        TestContext context =
            CreateContext(
                editorValidationResult:
                    new MarkdownEditorConfigurationValidationResult(
                        false,
                        expectedMessage),
                editor: editor);

        await context.ViewModel
            .OpenMnemoraCommand
            .ExecuteAsync(null);

        Assert.Equal(
            expectedMessage,
            context.ViewModel.CompletionErrorMessage);
        Assert.False(
            context.State.IsMarkdownEditorVerified);
        Assert.False(context.ViewModel.HasStorageError);
        Assert.True(context.ViewModel.HasEditorError);
        Assert.Equal(
            "Настроить редактор",
            context.ViewModel.BackButtonText);
        Assert.Equal(
            "Повторить",
            context.ViewModel.PrimaryActionText);
        Assert.Equal(0, context.Database.CallCount);
        Assert.Equal(0, context.Settings.CompleteCallCount);

        context.ViewModel.BackCommand.Execute(null);

        Assert.Equal(
            typeof(EditorSetupViewModel),
            context.Navigation.LastViewModelType);
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
        Action? onDatabaseInitialize = null,
        MarkdownEditorType editor =
            MarkdownEditorType.VisualStudioCode)
    {
        var state = new OnboardingState
        {
            UserName = "Ольга",
            StoragePath = _storagePath,
            MarkdownEditor = editor,
            VisualStudioCodePath =
                editor == MarkdownEditorType.VisualStudioCode
                    ? "Code.exe"
                    : null,
            ObsidianVaultPath =
                editor == MarkdownEditorType.Obsidian
                    ? _storagePath
                    : null,
            IsVisualStudioCodeVerified =
                editor == MarkdownEditorType.VisualStudioCode,
            IsObsidianVerified =
                editor == MarkdownEditorType.Obsidian,
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
                new StorageValidationService(
                    new TestPathProvider(
                        _localRootPath)),
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

        if (Directory.Exists(_localRootPath))
        {
            Directory.Delete(
                _localRootPath,
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

        public event EventHandler? CurrentViewModelChanged;

        public void NavigateTo<TViewModel>()
            where TViewModel : ViewModelBase
        {
            LastViewModelType =
                typeof(TViewModel);

            CurrentViewModelChanged?.Invoke(
                this,
                EventArgs.Empty);
        }
    }

    private sealed class TestPathProvider(
        string rootPath)
        : IMnemoraLocalPathProvider
    {
        public string RootPath => rootPath;

        public string TempPath => Path.Combine(
            RootPath,
            "Temp");
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
