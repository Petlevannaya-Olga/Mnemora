using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mnemora.Application;
using Mnemora.Application.Commands;
using Mnemora.Application.Queries;
using Mnemora.Application.Storage;
using Mnemora.Desktop.Ai;
using Mnemora.Desktop.Commands;
using Mnemora.Desktop.Development;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Notifications;
using Mnemora.Desktop.Queries;
using Mnemora.Desktop.Security;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Startup;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Home;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Pages;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.ViewModels.Shell;
using Mnemora.Desktop.ViewModels.Startup;
using Mnemora.Desktop.ViewModels.Topics;
using Mnemora.Desktop.Views.Startup;
using Mnemora.Infrastructure;

namespace Mnemora.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var startupWindow = _serviceProvider.GetRequiredService<StartupWindow>();
        bool? startupDialogResult = startupWindow.ShowDialog();
        StartupResult? startupResult = startupWindow.ViewModel.Result;

        if (startupDialogResult != true)
        {
            Shutdown();
            return;
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        var mainWindowViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        var onboardingState = _serviceProvider.GetRequiredService<OnboardingState>();
        var navigationService = _serviceProvider.GetRequiredService<INavigationService>();

        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        if (startupWindow.OpenOnboardingRequested)
        {
            onboardingState.IsOnboardingCompleted = false;
            onboardingState.IsMarkdownEditorVerified = false;

            if (string.IsNullOrWhiteSpace(
                    onboardingState.UserName))
            {
                navigationService.NavigateTo<WelcomeViewModel>();
            }
            else
            {
                navigationService.NavigateTo<StorageSetupViewModel>();
            }

            mainWindowViewModel.CompleteInitialization();
            mainWindow.Show();
            return;
        }

        if (startupResult is not { IsSuccess: true })
        {
            Shutdown();
            return;
        }

        if (startupResult.StorageIsConfigured && startupResult.EditorIsConfigured)
        {
            mainWindow.Show();

            // Для основного приложения сначала показываем лёгкий скелетон,
            // пока создаются оболочка и первая страница.
            await mainWindow.Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.ApplicationIdle);

            navigationService.NavigateTo<AppShellViewModel>();

            await mainWindow.Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.ContextIdle);

            mainWindowViewModel.CompleteInitialization();
            return;
        }

        if (startupResult.StorageIsConfigured && startupResult.WasOnboardingCompleted)
        {
            onboardingState.IsOnboardingCompleted = false;
            onboardingState.IsMarkdownEditorVerified = false;
            navigationService.NavigateTo<EditorSetupViewModel>();
        }
        else
        {
            onboardingState.IsOnboardingCompleted = false;
            navigationService.NavigateTo<WelcomeViewModel>();
        }

        // Онбординг подготавливаем до показа MainWindow, чтобы между стартовым
        // окном и первым экраном настройки не появлялась оболочка приложения.
        mainWindowViewModel.CompleteInitialization();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.AddDebug();
            builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<OnboardingState>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IMarkdownEditorService, MarkdownEditorService>();
        services.AddSingleton<IStoragePathProvider, StoragePathProvider>();
        services.AddSingleton<IStorageValidationService, StorageValidationService>();
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

        services.AddSingleton<IMnemoraLocalPathProvider, MnemoraLocalPathProvider>();
        services.AddSingleton<ILocalAppDataCleanupService, LocalAppDataCleanupService>();
        services.AddSingleton<IStorageTemporaryFilesCleanupService, StorageTemporaryFilesCleanupService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddTransient<StartupViewModel>();
        services.AddTransient<StartupWindow>();

#if DEBUG
        services.AddTransient<LibraryStressDataSeeder>();
#endif

        services.AddTransient<LibraryTopicViewModel>();
        services.AddTransient<LibrarySectionViewModel>();
        services.AddTransient<LibraryContainerViewModel>();
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
