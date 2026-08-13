using System.IO;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Ai;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Security;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class AiSetupViewModel
    : ViewModelBase
{
    private readonly IAiConnectionService
        _aiConnectionService;

    private readonly IApiKeyStore
        _apiKeyStore;

    private readonly INavigationService
        _navigationService;

    private string? _apiKey;

    private bool _isAiConfigured;

    private bool _isConnectionInvalid;

    private bool _isChecking;

    private bool _shouldPersistApiKey;

    private readonly OnboardingState _onboardingState;

    private string _connectionTitle =
        "Подключение не проверено";

    private string _connectionMessage =
        "Введите ключ и проверьте подключение";

    public AiSetupViewModel(
        IAiConnectionService aiConnectionService,
        IApiKeyStore apiKeyStore,
        INavigationService navigationService,
        OnboardingState onboardingState)
    {
        _aiConnectionService = aiConnectionService;
        _apiKeyStore = apiKeyStore;
        _navigationService = navigationService;
        _onboardingState = onboardingState;

        LoadApiKey();
    }

    public string? ApiKey
    {
        get => _apiKey;
        set
        {
            if (!SetProperty(ref _apiKey, value))
            {
                return;
            }

            _onboardingState.PendingApiKey = value;

            ResetConnectionState();

            CheckConnectionCommand
                .NotifyCanExecuteChanged();
        }
    }

    public bool IsAiConfigured
    {
        get => _isAiConfigured;
        private set => SetProperty(
            ref _isAiConfigured,
            value);
    }

    public bool IsConnectionInvalid
    {
        get => _isConnectionInvalid;
        private set => SetProperty(
            ref _isConnectionInvalid,
            value);
    }

    public bool IsChecking
    {
        get => _isChecking;
        private set
        {
            if (!SetProperty(ref _isChecking, value))
            {
                return;
            }

            CheckConnectionCommand
                .NotifyCanExecuteChanged();

            ProceedCommand
                .NotifyCanExecuteChanged();
        }
    }

    public string ConnectionTitle
    {
        get => _connectionTitle;
        private set => SetProperty(
            ref _connectionTitle,
            value);
    }

    public string ConnectionMessage
    {
        get => _connectionMessage;
        private set => SetProperty(
            ref _connectionMessage,
            value);
    }

    private bool CanCheckConnection()
    {
        return !IsChecking &&
               !string.IsNullOrWhiteSpace(ApiKey);
    }

    private bool CanProceed()
    {
        return !IsChecking;
    }

    [RelayCommand(CanExecute = nameof(CanCheckConnection))]
    private async Task CheckConnectionAsync(
        CancellationToken cancellationToken)
    {
        if (!CanCheckConnection())
        {
            return;
        }

        string apiKey = ApiKey!.Trim();

        IsChecking = true;
        IsAiConfigured = false;
        IsConnectionInvalid = false;
        _shouldPersistApiKey = false;

        ConnectionTitle =
            "Проверяем подключение";

        ConnectionMessage =
            "Отправляем безопасный запрос в OpenAI";

        try
        {
            AiConnectionCheckResult result =
                await _aiConnectionService.CheckAsync(
                    apiKey,
                    cancellationToken);

            if (!result.IsSuccess)
            {
                SetConnectionFailure(
                    result.Message);

                return;
            }

            _shouldPersistApiKey =
                result.ShouldPersist;

            IsAiConfigured = true;
            IsConnectionInvalid = false;

            ConnectionTitle =
                "Подключение установлено";

            ConnectionMessage =
                result.Message;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            ResetConnectionState();
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanProceed))]
    private void Proceed()
    {
        if (IsAiConfigured)
        {
            _onboardingState.PendingApiKey =
                ApiKey!.Trim();

            _onboardingState.IsAiConfigured = true;
        }
        else
        {
            _onboardingState.PendingApiKey = null;
            _onboardingState.IsAiConfigured = false;
        }

        _navigationService.NavigateTo<CompletionSetupViewModel>();
    }

    [RelayCommand]
    private void Back()
    {
        _navigationService
            .NavigateTo<StorageSetupViewModel>();
    }
    
    private void LoadApiKey()
    {
        if (!string.IsNullOrWhiteSpace(
                _onboardingState.PendingApiKey))
        {
            _apiKey =
                _onboardingState.PendingApiKey;

            return;
        }

        // Загрузка окончательно сохранённого ключа
        // из DpapiApiKeyStore.
        LoadSavedApiKey();
    }

    private void LoadSavedApiKey()
    {
        if (_onboardingState.PendingApiKey is not null)
        {
            _apiKey = _onboardingState.PendingApiKey;

            _isAiConfigured = _onboardingState.IsAiConfigured;

            if (_isAiConfigured)
            {
                _connectionTitle = "Подключение установлено";

                _connectionMessage = "API-ключ уже проверен";
            }

            return;
        }

        try
        {
            _apiKey = _apiKeyStore.Load();

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _onboardingState.IsAiConfigured = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return;
            }

            _isAiConfigured = true;
            _onboardingState.IsAiConfigured = true;
            _shouldPersistApiKey = false;
            _onboardingState.PendingApiKey = ApiKey;

            _connectionTitle = "Подключение настроено";

            _connectionMessage = "Сохранённый API-ключ будет использован Mnemora";
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or CryptographicException
                      or PlatformNotSupportedException)
        {
            _isConnectionInvalid = true;
            _shouldPersistApiKey = false;
            _onboardingState.IsAiConfigured = false;

            _connectionTitle = "Не удалось прочитать API-ключ";

            _connectionMessage = "Введите ключ заново и проверьте подключение";
        }
    }

    private void ResetConnectionState()
    {
        _shouldPersistApiKey = false;

        IsAiConfigured = false;
        IsConnectionInvalid = false;

        ConnectionTitle = "Подключение не проверено";

        ConnectionMessage = "Введите ключ и проверьте подключение";

        _onboardingState.IsAiConfigured = false;
    }

    private void SetConnectionFailure(
        string message)
    {
        _shouldPersistApiKey = false;

        IsAiConfigured = false;
        IsConnectionInvalid = true;

        ConnectionTitle =
            "Не удалось подключиться";

        ConnectionMessage = message;
    }
}