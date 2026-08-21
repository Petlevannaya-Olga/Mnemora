using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Startup;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Startup;

public sealed partial class StartupViewModel(IStartupService startupService) : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;
    private int _progress;
    private string _title = "Запускаем Mnemora";
    private string? _details = "Подготавливаем приложение";
    private bool _isRunning;
    private string? _errorMessage;

    public int Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string? Details
    {
        get => _details;
        private set => SetProperty(ref _details, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetProperty(ref _isRunning, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanRetry));
            OnPropertyChanged(nameof(CanRepairStorage));
            OnPropertyChanged(nameof(ErrorHint));
            OnPropertyChanged(nameof(ErrorActionText));
            OnPropertyChanged(nameof(ErrorActionButtonWidth));
            OpenOnboardingCommand.NotifyCanExecuteChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(CanRetry));
            OnPropertyChanged(nameof(CanRepairStorage));
            OnPropertyChanged(nameof(ErrorHint));
            OnPropertyChanged(nameof(ErrorActionText));
            OnPropertyChanged(nameof(ErrorActionButtonWidth));
            OpenOnboardingCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanRetry => HasError && !IsRunning;
    public bool CanRepairStorage =>
        CanRetry &&
        Result?.CanRepairStorage == true;

    public string ErrorHint => CanRepairStorage
        ? "Материалы не будут изменены. Можно восстановить служебные настройки."
        : Result?.StorageFailureKind ==
          StorageValidationFailureKind.StorageVersionIsNewer
            ? "Обновите Mnemora или выберите другое хранилище."
            : "Повторите проверку или измените настройки приложения.";

    public string ErrorActionText =>
        CanRepairStorage
            ? "Восстановить"
            : "Повторить";

    public double ErrorActionButtonWidth =>
        CanRepairStorage
            ? 190
            : 138;

    public StartupResult? Result { get; private set; }

    public event EventHandler? StartupSucceeded;
    public event EventHandler? OnboardingRequested;
    public event EventHandler? CloseRequested;

    public Task RunAsync() =>
        RunCoreAsync(
            repairStorage: false);

    private async Task RunCoreAsync(
        bool repairStorage)
    {
        if (IsRunning)
        {
            return;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        ErrorMessage = null;
        Result = null;
        NotifyErrorActionChanged();
        Progress = 0;
        Title = repairStorage
            ? "Восстанавливаем хранилище"
            : "Запускаем Mnemora";
        Details = repairStorage
            ? "Материалы не будут изменены"
            : "Подготавливаем приложение";
        IsRunning = true;

        try
        {
            var progress = new InlineProgress<StartupProgress>(ApplyProgress);
            StartupResult result = repairStorage
                ? await startupService.RepairStorageAsync(
                    progress,
                    _cancellationTokenSource.Token)
                : await startupService.InitializeAsync(
                    progress,
                    _cancellationTokenSource.Token);

            Result = result;
            NotifyErrorActionChanged();

            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Не удалось запустить Mnemora.";
                Title = "Не удалось завершить запуск";
                Details = "Исправьте проблему или попробуйте ещё раз";
                return;
            }

            StartupSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            // ignore
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            Title = "Не удалось завершить запуск";
            Details = "Произошла непредвиденная ошибка";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync()
    {
        return CanRetry
            ? RunCoreAsync(CanRepairStorage)
            : Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private void OpenOnboarding()
    {
        OnboardingRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    [RelayCommand]
    private void Close()
    {
        _cancellationTokenSource?.Cancel();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyProgress(StartupProgress progress)
    {
        Progress = Math.Clamp(progress.Percent, 0, 100);
        Title = progress.Title;
        Details = progress.Details;
    }

    private void NotifyErrorActionChanged()
    {
        OnPropertyChanged(nameof(CanRepairStorage));
        OnPropertyChanged(nameof(ErrorHint));
        OnPropertyChanged(nameof(ErrorActionText));
        OnPropertyChanged(nameof(ErrorActionButtonWidth));
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
