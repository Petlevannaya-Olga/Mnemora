using System.IO;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Security;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class CompletionSetupViewModel(
    INavigationService navigationService,
    OnboardingState onboardingState,
    IApiKeyStore apiKeyStore,
    ISettingsService settingsService)
    : ViewModelBase
{
    public string UserName =>
        onboardingState.UserName?.Trim()
        ?? string.Empty;

    public string StorageStatus => "Папка для материалов выбрана";

    public bool IsAiConfigured =>
        onboardingState.IsAiConfigured;

    public bool IsAiSkipped =>
        !IsAiConfigured;

    public string AiStatus =>
        IsAiConfigured
            ? "Подключение установлено"
            : "Не подключён — можно настроить позже";

    [RelayCommand]
    private void Back()
    {
        navigationService.NavigateTo<AiSetupViewModel>();
    }

    [RelayCommand]
    private async Task OpenMnemoraAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            if (onboardingState.IsAiConfigured)
            {
                if (string.IsNullOrWhiteSpace(
                        onboardingState.PendingApiKey))
                {
                    return;
                }

                apiKeyStore.Save(
                    onboardingState.PendingApiKey.Trim());
            }

            await settingsService.CompleteOnboardingAsync(
                onboardingState.IsAiConfigured,
                cancellationToken);

            onboardingState.IsOnboardingCompleted = true;
            onboardingState.PendingApiKey = null;

            // Здесь позже откроем главный экран Mnemora.
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Завершение отменено.
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or CryptographicException
                      or PlatformNotSupportedException)
        {
            // Позже выведем ошибку на экран.
            // Мастер при этом остаётся незавершённым.
        }
    }
}