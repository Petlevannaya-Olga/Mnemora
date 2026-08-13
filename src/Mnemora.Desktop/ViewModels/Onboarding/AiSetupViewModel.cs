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

    private string _connectionTitle =
        "Подключение не проверено";

    private string _connectionMessage =
        "Введите ключ и проверьте подключение";

    public AiSetupViewModel(
        IAiConnectionService aiConnectionService,
        IApiKeyStore apiKeyStore,
        INavigationService navigationService)
    {
        _aiConnectionService =
            aiConnectionService;

        _apiKeyStore =
            apiKeyStore;

        _navigationService =
            navigationService;

        LoadSavedApiKey();
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

    [RelayCommand(
        CanExecute = nameof(CanCheckConnection))]
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

    [RelayCommand(
        CanExecute = nameof(CanProceed))]
    private void Proceed()
    {
        try
        {
            if (IsAiConfigured &&
                _shouldPersistApiKey)
            {
                string apiKey =
                    ApiKey!.Trim();

                _apiKeyStore.Save(apiKey);

                _shouldPersistApiKey = false;
            }
            else if (!IsAiConfigured)
            {
                _apiKeyStore.Delete();
            }
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or CryptographicException
                      or PlatformNotSupportedException)
        {
            SetConnectionFailure(
                "Не удалось сохранить API-ключ в защищённом хранилище.");

            return;
        }

        // Добавим после создания ViewModel четвёртого шага:
        // _navigationService.NavigateTo<...>();
    }

    [RelayCommand]
    private void Back()
    {
        _navigationService
            .NavigateTo<StorageSetupViewModel>();
    }

    private void LoadSavedApiKey()
    {
        try
        {
            _apiKey =
                _apiKeyStore.Load();

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return;
            }

            _isAiConfigured = true;
            _shouldPersistApiKey = false;

            _connectionTitle =
                "Подключение настроено";

            _connectionMessage =
                "Сохранённый API-ключ будет использован Mnemora";
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or CryptographicException
                      or PlatformNotSupportedException)
        {
            _isConnectionInvalid = true;
            _shouldPersistApiKey = false;

            _connectionTitle =
                "Не удалось прочитать API-ключ";

            _connectionMessage =
                "Введите ключ заново и проверьте подключение";
        }
    }

    private void ResetConnectionState()
    {
        _shouldPersistApiKey = false;

        IsAiConfigured = false;
        IsConnectionInvalid = false;

        ConnectionTitle =
            "Подключение не проверено";

        ConnectionMessage =
            "Введите ключ и проверьте подключение";
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