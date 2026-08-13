using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Shell;

namespace Mnemora.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        await LoadSettingsAsync(_serviceProvider);

        // Получаем окно после загрузки настроек,
        // чтобы ViewModel создавались с заполненным OnboardingState.
        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        INavigationService navigationService = _serviceProvider.GetRequiredService<INavigationService>();

        navigationService.NavigateTo<WelcomeViewModel>();

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
            AppSettings settings =
                await settingsService.LoadAsync();

            onboardingState.UserName =
                string.IsNullOrWhiteSpace(settings.UserName)
                    ? null
                    : settings.UserName.Trim();

            onboardingState.StoragePath =
                string.IsNullOrWhiteSpace(settings.StoragePath)
                    ? null
                    : settings.StoragePath.Trim();
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or JsonException)
        {
            // Повреждённые или недоступные настройки
            // не должны блокировать запуск приложения.
            onboardingState.UserName = null;
        }
    }

    private static void ConfigureServices(
        IServiceCollection services)
    {
        services.AddSingleton<OnboardingState>();

        services.AddSingleton<ISettingsService, JsonSettingsService>();

        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<IFolderPickerService, FolderPickerService>();

        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<ProfileSetupViewModel>();
        services.AddTransient<StorageSetupViewModel>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
    }
}