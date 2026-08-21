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
    private CompletionIssue _completionIssue;

    public bool IsCompleting
    {
        get => _isCompleting;
        private set
        {
            if (!SetProperty(ref _isCompleting, value)) return;

            BackCommand.NotifyCanExecuteChanged();
            OpenMnemoraCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PrimaryActionText));
        }
    }

    public string? CompletionErrorMessage
    {
        get => _completionErrorMessage;
        private set
        {
            if (!SetProperty(ref _completionErrorMessage, value)) return;

            OnPropertyChanged(nameof(HasCompletionError));
            OnPropertyChanged(nameof(CompletionTitle));
            OnPropertyChanged(nameof(CompletionSubtitle));
            OnPropertyChanged(nameof(PrimaryActionText));
        }
    }

    public bool HasCompletionError => !string.IsNullOrWhiteSpace(CompletionErrorMessage);

    public string UserName => onboardingState.UserName?.Trim() ?? string.Empty;

    public string CompletionTitle =>
        HasCompletionError
            ? "Проверьте настройки"
            : $"Всё готово, {UserName}!";

    public string CompletionSubtitle =>
        CompletionErrorMessage ??
        "Mnemora настроена и готова к работе";

    public bool HasProfileError =>
        _completionIssue == CompletionIssue.Profile;

    public bool HasStorageError =>
        _completionIssue is
            CompletionIssue.Storage or
            CompletionIssue.StorageRepairable or
            CompletionIssue.StorageVersionUnsupported;

    public bool CanRepairStorage =>
        _completionIssue ==
        CompletionIssue.StorageRepairable;

    public bool HasEditorError =>
        _completionIssue == CompletionIssue.Editor;

    public bool HasAiError =>
        _completionIssue == CompletionIssue.Ai;

    public string BackButtonText => _completionIssue switch
    {
        CompletionIssue.Profile => "Изменить профиль",
        CompletionIssue.Storage or
            CompletionIssue.StorageRepairable or
            CompletionIssue.StorageVersionUnsupported =>
            "Изменить хранилище",
        CompletionIssue.Editor => "Настроить редактор",
        CompletionIssue.Ai => "Настроить ИИ",
        _ => "Назад",
    };

    public string PrimaryActionText =>
        IsCompleting
            ? "Проверяем..."
            : CanRepairStorage
                ? "Восстановить хранилище"
            : HasCompletionError
                ? "Повторить"
                : "Открыть Mnemora";

    public string StorageStatus =>
        string.IsNullOrWhiteSpace(onboardingState.StoragePath)
            ? "Не выбрано"
            : onboardingState.StoragePath.Trim();

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
        switch (_completionIssue)
        {
            case CompletionIssue.Profile:
                navigationService.NavigateTo<ProfileSetupViewModel>();
                break;

            case CompletionIssue.Storage:
            case CompletionIssue.StorageRepairable:
            case CompletionIssue.StorageVersionUnsupported:
                navigationService.NavigateTo<StorageSetupViewModel>();
                break;

            case CompletionIssue.Editor:
                navigationService.NavigateTo<EditorSetupViewModel>();
                break;

            default:
                navigationService.NavigateTo<AiSetupViewModel>();
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task OpenMnemoraAsync(CancellationToken cancellationToken)
    {
        bool shouldRepairStorage =
            CanRepairStorage;

        IsCompleting = true;
        SetCompletionError(
            null,
            CompletionIssue.None);

        try
        {
            if (string.IsNullOrWhiteSpace(
                    onboardingState.UserName))
            {
                SetCompletionError(
                    "Имя пользователя не указано. Вернитесь к шагу профиля.",
                    CompletionIssue.Profile);
                return;
            }

            if (shouldRepairStorage &&
                !await TryRepairStorageAsync(
                    cancellationToken))
            {
                return;
            }

            if (!await ValidateStorageAsync(
                    cancellationToken))
            {
                return;
            }

            if (!ValidateEditor())
            {
                return;
            }

            if (onboardingState.IsAiConfigured && string.IsNullOrWhiteSpace(onboardingState.PendingApiKey))
            {
                SetCompletionError(
                    "Не найден проверенный API-ключ. Вернитесь к шагу ИИ и проверьте подключение.",
                    CompletionIssue.Ai);
                return;
            }

            var databaseResult = await databaseInitializer.InitializeAsync(cancellationToken);

            if (databaseResult.IsFailure)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    SetCompletionError(
                        databaseResult.Error.Message,
                        CompletionIssue.General);
                }

                return;
            }

            // Повторная проверка закрывает случай, когда папки удалили
            // во время инициализации базы данных.
            if (!await ValidateStorageAsync(
                    cancellationToken) ||
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
            SetCompletionError(
                "Не удалось завершить настройку. Проверьте доступ к файлам и попробуйте ещё раз.",
                CompletionIssue.General);
        }
        finally
        {
            IsCompleting = false;
        }
    }

    private void SetCompletionError(
        string? message,
        CompletionIssue issue)
    {
        bool issueChanged =
            _completionIssue != issue;

        _completionIssue = issue;
        CompletionErrorMessage = message;

        if (!issueChanged)
        {
            return;
        }

        OnPropertyChanged(nameof(HasProfileError));
        OnPropertyChanged(nameof(HasStorageError));
        OnPropertyChanged(nameof(CanRepairStorage));
        OnPropertyChanged(nameof(HasEditorError));
        OnPropertyChanged(nameof(HasAiError));
        OnPropertyChanged(nameof(BackButtonText));
        OnPropertyChanged(nameof(PrimaryActionText));
    }

    private async Task<bool> TryRepairStorageAsync(
        CancellationToken cancellationToken)
    {
        StorageValidationResult result =
            await storageValidationService.RepairAsync(
                onboardingState.StoragePath,
                cancellationToken);

        if (result.IsValid)
        {
            onboardingState.StoragePath =
                result.NormalizedPath;

            return true;
        }

        SetStorageError(result);
        return false;
    }

    private async Task<bool> ValidateStorageAsync(
        CancellationToken cancellationToken)
    {
        StorageValidationResult result =
            await storageValidationService.PrepareAsync(
                onboardingState.StoragePath,
                cancellationToken);

        if (result.IsValid)
        {
            onboardingState.StoragePath =
                result.NormalizedPath;

            return true;
        }

        SetStorageError(result);

        return false;
    }

    private void SetStorageError(
        StorageValidationResult result)
    {
        CompletionIssue issue =
            result.FailureKind switch
            {
                StorageValidationFailureKind.MarkerMissing or
                    StorageValidationFailureKind.MarkerCorrupted =>
                    CompletionIssue.StorageRepairable,

                StorageValidationFailureKind.StorageVersionIsNewer or
                    StorageValidationFailureKind.StorageVersionUnsupported =>
                    CompletionIssue.StorageVersionUnsupported,

                _ => CompletionIssue.Storage,
            };

        SetCompletionError(
            result.ErrorMessage ??
            "Хранилище Mnemora недоступно.",
            issue);
    }

    private bool ValidateEditor()
    {
        if (!onboardingState.IsMarkdownEditorVerified)
        {
            SetCompletionError(
                "Markdown-редактор не проверен. Вернитесь к шагу редактора и повторите проверку.",
                CompletionIssue.Editor);

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

        SetCompletionError(
            result.Message,
            CompletionIssue.Editor);

        return false;
    }

    private enum CompletionIssue
    {
        None,
        Profile,
        Storage,
        StorageRepairable,
        StorageVersionUnsupported,
        Editor,
        Ai,
        General,
    }
}
