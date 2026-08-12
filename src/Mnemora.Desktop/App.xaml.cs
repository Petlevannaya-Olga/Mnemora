using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Shell;

namespace Mnemora.Desktop;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        ConfigureServices(builder.Services);
        _host = builder.Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host.Start();

        MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();

        MainWindow = mainWindow;

        INavigationService navigationService =
            _host.Services
                .GetRequiredService<INavigationService>();

        navigationService.NavigateTo<ProfileSetupViewModel>();

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync()
            .GetAwaiter()
            .GetResult();

        _host.Dispose();

        base.OnExit(e);
    }

    private static void ConfigureServices(
        IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddTransient<ProfileSetupViewModel>();
        services.AddTransient<StorageSetupViewModel>();

        services.AddSingleton<ISettingsService, JsonSettingsService>();
    }
}