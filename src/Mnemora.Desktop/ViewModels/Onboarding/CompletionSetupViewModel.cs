using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Database;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Security;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Shell;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class CompletionSetupViewModel(
    INavigationService navigationService,
    OnboardingState onboardingState,
    IApiKeyStore apiKeyStore,
    ISettingsService settingsService,
    IDatabaseInitializer databaseInitializer,
    IStorageValidationService storageValidationService,
    IMarkdownEditorService markdownEditorService) : ViewModelBase
{
    private bool _isCompleting;
    private string? _completionErrorMessage;

    public bool IsCompleting
    {
        get => _isCompleting;
        private set
        {
            if (!SetProperty(ref _isCompleting, value)) return;

            BackCommand.NotifyCanExecuteChanged();
            OpenMnemoraCommand.NotifyCanExecuteChanged();
        }
    }

    public string? CompletionErrorMessage
    {
        get => _completionErrorMessage;
        private set
        {
            if (!SetProperty(ref _completionErrorMessage, value)) return;

            OnPropertyChanged(nameof(HasCompletionError));
        }
    }

    public bool HasCompletionError => !string.IsNullOrWhiteSpace(CompletionErrorMessage);

    public string UserName => onboardingState.UserName?.Trim() ?? string.Empty;

    public string StorageStatus => "Папка для материалов выбрана";

    public bool IsEditorConfigured =>
        onboardingState.MarkdownEditor is not null;

    public string EditorStatus => onboardingState.MarkdownEditor switch
    {
        MarkdownEditorType.VisualStudioCode => "Visual Studio Code",
        MarkdownEditorType.Obsidian => "Obsidian",
        _ => "Не настроен",
    };

    public bool IsAiConfigured => onboardingState.IsAiConfigured;

    public bool IsAiSkipped => !IsAiConfigured;

    public string AiStatus => IsAiConfigured ? "Подключение установлено" : "Не подключён — можно настроить позже";

    private bool CanInteract() => !IsCompleting;

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private void Back()
    {
        navigationService.NavigateTo<AiSetupViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task OpenMnemoraAsync(CancellationToken cancellationToken)
    {
        IsCompleting = true;
        CompletionErrorMessage = null;

        try
        {
            if (string.IsNullOrWhiteSpace(
                    onboardingState.UserName))
            {
                CompletionErrorMessage =
                    "Имя пользователя не указано. Вернитесь к первому шагу настройки.";
                return;
            }

            if (!ValidateStorage())
            {
                return;
            }

            if (!ValidateEditor())
            {
                return;
            }

            if (onboardingState.IsAiConfigured && string.IsNullOrWhiteSpace(onboardingState.PendingApiKey))
            {
                CompletionErrorMessage = "Не найден проверенный API-ключ. Вернитесь на предыдущий шаг и проверьте подключение.";
                return;
            }

            var databaseResult = await databaseInitializer.InitializeAsync(cancellationToken);

            if (databaseResult.IsFailure)
            {
                if (!cancellationToken.IsCancellationRequested) CompletionErrorMessage = databaseResult.Error.Message;

                return;
            }

            // Повторная проверка закрывает случай, когда папки удалили
            // во время инициализации базы данных.
            if (!ValidateStorage() ||
                !ValidateEditor())
            {
                return;
            }

            if (onboardingState.IsAiConfigured)
            {
                apiKeyStore.Save(onboardingState.PendingApiKey!.Trim());
            }

            await settingsService.CompleteOnboardingAsync(onboardingState.IsAiConfigured, cancellationToken);

            onboardingState.IsOnboardingCompleted = true;
            onboardingState.PendingApiKey = null;

            navigationService.NavigateTo<AppShellViewModel>();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ignore
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or NotSupportedException or JsonException)
        {
            CompletionErrorMessage = "Не удалось завершить настройку. Проверьте доступ к файлам и попробуйте ещё раз.";
        }
        finally
        {
            IsCompleting = false;
        }
    }

    private bool ValidateStorage()
    {
        StorageValidationResult result =
            storageValidationService.ValidateConfigured(
                onboardingState.StoragePath);

        if (result.IsValid)
        {
            onboardingState.StoragePath =
                result.NormalizedPath;

            return true;
        }

        CompletionErrorMessage =
            result.ErrorMessage ??
            "Хранилище Mnemora недоступно. Вернитесь к шагу выбора папки.";

        return false;
    }

    private bool ValidateEditor()
    {
        if (!onboardingState.IsMarkdownEditorVerified)
        {
            CompletionErrorMessage =
                "Markdown-редактор не проверен. Вернитесь к шагу редактора и повторите проверку.";

            return false;
        }

        MarkdownEditorConfigurationValidationResult result =
            markdownEditorService.ValidateConfiguration(
                onboardingState.MarkdownEditor,
                onboardingState.VisualStudioCodePath,
                onboardingState.ObsidianVaultPath);

        if (result.IsValid)
        {
            return true;
        }

        onboardingState.IsMarkdownEditorVerified =
            false;

        CompletionErrorMessage =
            result.Message;

        return false;
    }
}
