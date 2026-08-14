using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mnemora.Application;
using Mnemora.Application.Commands;
using Mnemora.Application.Queries;
using Mnemora.Desktop.Ai;
using Mnemora.Desktop.Commands;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Queries;
using Mnemora.Desktop.Security;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Home;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.ViewModels.Shell;
using Mnemora.Desktop.ViewModels.Topics;
using Mnemora.Infrastructure;
using Mnemora.Infrastructure.Persistence;

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
        await InitializeDatabaseAsync(_serviceProvider);

        // Получаем окно после загрузки настроек,
        // чтобы ViewModel создавались с заполненным OnboardingState.
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        var navigationService = _serviceProvider.GetRequiredService<INavigationService>();
        var onboardingState = _serviceProvider.GetRequiredService<OnboardingState>();

        if (onboardingState.IsOnboardingCompleted)
        {
            navigationService.NavigateTo<AppShellViewModel>();
        }
        else
        {
            navigationService.NavigateTo<WelcomeViewModel>();
        }

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static async Task LoadSettingsAsync(
        IServiceProvider serviceProvider)
    {
        ISettingsService settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        OnboardingState onboardingState = serviceProvider.GetRequiredService<OnboardingState>();

        try
        {
            AppSettings settings = await settingsService.LoadAsync();

            onboardingState.UserName =
                string.IsNullOrWhiteSpace(settings.UserName)
                    ? null
                    : settings.UserName.Trim();

            onboardingState.StoragePath =
                string.IsNullOrWhiteSpace(settings.StoragePath)
                    ? null
                    : settings.StoragePath.Trim();

            onboardingState.IsAiConfigured = settings.IsAiConfigured;

            onboardingState.IsOnboardingCompleted = settings.IsOnboardingCompleted;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or JsonException)
        {
            onboardingState.UserName = null;
            onboardingState.StoragePath = null;
            onboardingState.IsAiConfigured = false;
            onboardingState.IsOnboardingCompleted = false;
            onboardingState.PendingApiKey = null;
        }
    }

    private static async Task InitializeDatabaseAsync(
        IServiceProvider serviceProvider)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddInfrastructure();
        services.AddApplication();

        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<OnboardingState>();

        services.AddSingleton<ISettingsService, JsonSettingsService>();

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IPageNavigationService, PageNavigationService>();

        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IFolderLauncherService, FolderLauncherService>();

        services.AddSingleton<IApiKeyStore, DpapiApiKeyStore>();

        services.AddSingleton<IAiConnectionService, DevelopmentAiConnectionService>();

        services.AddSingleton<IDialogService, DialogService>();
        
        services.AddTransient<CreateTopicDialogViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<CreateSectionDialogViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddSingleton<AppShellViewModel>();

        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<ProfileSetupViewModel>();
        services.AddTransient<StorageSetupViewModel>();
        services.AddTransient<AiSetupViewModel>();
        services.AddTransient<CompletionSetupViewModel>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
    }
}