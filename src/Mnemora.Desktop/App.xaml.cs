using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mnemora.Application;
using Mnemora.Application.Commands;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.Queries;
using Mnemora.Application.Storage;
using Mnemora.Desktop.Ai;
using Mnemora.Desktop.Commands;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Notifications;
using Mnemora.Desktop.Queries;
using Mnemora.Desktop.Security;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Home;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Pages;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.ViewModels.Shell;
using Mnemora.Desktop.ViewModels.Topics;
using Mnemora.Infrastructure;

namespace Mnemora.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        await LoadSettingsAsync(_serviceProvider);

        var onboardingState = _serviceProvider.GetRequiredService<OnboardingState>();

        bool wasOnboardingCompleted =
            onboardingState.IsOnboardingCompleted;

        bool storageIsConfigured =
            wasOnboardingCompleted &&
            !string.IsNullOrWhiteSpace(onboardingState.StoragePath);

        bool editorIsConfigured =
            HasEditorConfiguration(onboardingState);

        if (storageIsConfigured)
        {
            var databaseInitializer = _serviceProvider.GetRequiredService<IDatabaseInitializer>();
            var databaseResult = await databaseInitializer.InitializeAsync();

            if (databaseResult.IsFailure)
            {
                MessageBox.Show(
                    databaseResult.Error.Message,
                    "Mnemora",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            await CheckMaterialContentConsistencyAsync(_serviceProvider);
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var navigationService = _serviceProvider.GetRequiredService<INavigationService>();

        if (storageIsConfigured && editorIsConfigured)
        {
            navigationService.NavigateTo<AppShellViewModel>();
        }
        else if (storageIsConfigured && wasOnboardingCompleted)
        {
            // Пользователь уже проходил старый онбординг,
            // но обязательной настройки Markdown-редактора ещё нет.
            onboardingState.IsOnboardingCompleted = false;
            onboardingState.IsMarkdownEditorVerified = false;
            navigationService.NavigateTo<EditorSetupViewModel>();
        }
        else
        {
            onboardingState.IsOnboardingCompleted = false;
            navigationService.NavigateTo<WelcomeViewModel>();
        }

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static bool HasEditorConfiguration(
        OnboardingState onboardingState)
    {
        return onboardingState.MarkdownEditor switch
        {
            MarkdownEditorType.VisualStudioCode =>
                !string.IsNullOrWhiteSpace(
                    onboardingState.VisualStudioCodePath),

            MarkdownEditorType.Obsidian =>
                !string.IsNullOrWhiteSpace(
                    onboardingState.ObsidianVaultPath),

            _ => false,
        };
    }

    private static async Task LoadSettingsAsync(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        var onboardingState = serviceProvider.GetRequiredService<OnboardingState>();

        try
        {
            var settings = await settingsService.LoadAsync();

            onboardingState.UserName =
                string.IsNullOrWhiteSpace(settings.UserName)
                    ? null
                    : settings.UserName.Trim();

            onboardingState.StoragePath =
                string.IsNullOrWhiteSpace(settings.StoragePath)
                    ? null
                    : settings.StoragePath.Trim();

            onboardingState.MarkdownEditor =
                settings.MarkdownEditor;

            onboardingState.VisualStudioCodePath =
                string.IsNullOrWhiteSpace(settings.VisualStudioCodePath)
                    ? null
                    : settings.VisualStudioCodePath.Trim();

            onboardingState.ObsidianVaultPath =
                string.IsNullOrWhiteSpace(settings.ObsidianVaultPath)
                    ? null
                    : settings.ObsidianVaultPath.Trim();

            onboardingState.IsMarkdownEditorVerified = false;
            onboardingState.IsAiConfigured = settings.IsAiConfigured;
            onboardingState.IsOnboardingCompleted = settings.IsOnboardingCompleted;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            onboardingState.UserName = null;
            onboardingState.StoragePath = null;
            onboardingState.MarkdownEditor = null;
            onboardingState.VisualStudioCodePath = null;
            onboardingState.ObsidianVaultPath = null;
            onboardingState.IsMarkdownEditorVerified = false;
            onboardingState.IsAiConfigured = false;
            onboardingState.IsOnboardingCompleted = false;
            onboardingState.PendingApiKey = null;
        }
    }

    private static async Task CheckMaterialContentConsistencyAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var consistencyService =
            scope.ServiceProvider.GetRequiredService<IMaterialContentConsistencyService>();

        var result = await consistencyService.CheckAndRepairAsync(CancellationToken.None);

        if (result.IsFailure)
        {
            MessageBox.Show(
                result.Error.Message,
                "Mnemora",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var report = result.Value;

        if (report.QuarantinedDirectoryCount == 0 &&
            report.MissingContentCount == 0 &&
            report.InvalidDirectoryCount == 0)
        {
            return;
        }

        MessageBox.Show(
            $"""
             Проверка хранилища завершена.

             Перемещено в папку восстановления: {report.QuarantinedDirectoryCount}
             Материалов с отсутствующими файлами: {report.MissingContentCount}
             Неизвестных папок оставлено без изменений: {report.InvalidDirectoryCount}
             """,
            "Mnemora",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<OnboardingState>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IMarkdownEditorService, MarkdownEditorService>();
        services.AddSingleton<IStoragePathProvider, StoragePathProvider>();
        services.AddSingleton(TimeProvider.System);

        services.AddInfrastructure();
        services.AddApplication();

        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();

        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IPageNavigationService, PageNavigationService>();

        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IFolderLauncherService, FolderLauncherService>();

        services.AddSingleton<IApiKeyStore, DpapiApiKeyStore>();
        services.AddSingleton<IAiConnectionService, DevelopmentAiConnectionService>();

        services.AddSingleton<IDialogService, DialogService>();

        services.AddTransient<LibraryTopicViewModel>();
        services.AddTransient<LibrarySectionViewModel>();
        services.AddTransient<AllMaterialsViewModel>();
        services.AddTransient<LibraryOverviewViewModel>();
        services.AddTransient<CreateMaterialViewModel>();
        services.AddTransient<LibraryManagementViewModel>();
        services.AddTransient<DeleteTopicDialogViewModel>();
        services.AddTransient<EditTopicDialogViewModel>();
        services.AddTransient<SelectSectionIconDialogViewModel>();
        services.AddTransient<SelectTopicIconDialogViewModel>();
        services.AddTransient<CreateTopicDialogViewModel>();
        services.AddTransient<EditSectionDialogViewModel>();
        services.AddTransient<DeleteSectionDialogViewModel>();
        services.AddTransient<PracticeViewModel>();
        services.AddTransient<TrainingViewModel>();
        services.AddTransient<PlanViewModel>();
        services.AddTransient<ProgressViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<CreateSectionDialogViewModel>();
        services.AddTransient<HomeViewModel>();

        services.AddSingleton<AppShellViewModel>();

        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<ProfileSetupViewModel>();
        services.AddTransient<StorageSetupViewModel>();
        services.AddTransient<EditorSetupViewModel>();
        services.AddTransient<AiSetupViewModel>();
        services.AddTransient<CompletionSetupViewModel>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
