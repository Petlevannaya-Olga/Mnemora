using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
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
    private bool _isCompleting;

    private string? _completionErrorMessage;

    public bool IsCompleting
    {
        get => _isCompleting;
        private set
        {
            if (!SetProperty(ref _isCompleting, value))
            {
                return;
            }

            OpenMnemoraCommand.NotifyCanExecuteChanged();
        }
    }

    public string? CompletionErrorMessage
    {
        get => _completionErrorMessage;
        private set
        {
            if (!SetProperty(
                    ref _completionErrorMessage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasCompletionError));
        }
    }

    public bool HasCompletionError =>
        !string.IsNullOrWhiteSpace(
            CompletionErrorMessage);

    private bool CanOpenMnemora()
    {
        return !IsCompleting;
    }

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

    [RelayCommand(
        CanExecute = nameof(CanOpenMnemora))]
    private async Task OpenMnemoraAsync(
        CancellationToken cancellationToken)
    {
        IsCompleting = true;
        CompletionErrorMessage = null;

        try
        {
            if (onboardingState.IsAiConfigured)
            {
                if (string.IsNullOrWhiteSpace(
                        onboardingState.PendingApiKey))
                {
                    CompletionErrorMessage =
                        "Не найден проверенный API-ключ. Вернитесь на предыдущий шаг и проверьте подключение.";

                    return;
                }

                apiKeyStore.Save(
                    onboardingState.PendingApiKey.Trim());
            }

            await settingsService
                .CompleteOnboardingAsync(
                    onboardingState.IsAiConfigured,
                    cancellationToken);

            onboardingState.IsOnboardingCompleted =
                true;

            onboardingState.PendingApiKey = null;

            // Переход на главный экран добавим
            // после создания его ViewModel.
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Отмена закрытия или завершения не считается ошибкой.
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or CryptographicException
                      or PlatformNotSupportedException
                      or JsonException)
        {
            CompletionErrorMessage =
                "Не удалось завершить настройку. Проверьте доступ к файлам и попробуйте ещё раз.";
        }
        finally
        {
            IsCompleting = false;
        }
    }
}