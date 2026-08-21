using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Startup;
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
            OpenOnboardingCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanRetry => HasError && !IsRunning;
    public StartupResult? Result { get; private set; }

    public event EventHandler? StartupSucceeded;
    public event EventHandler? OnboardingRequested;
    public event EventHandler? CloseRequested;

    public async Task RunAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        ErrorMessage = null;
        Result = null;
        Progress = 0;
        Title = "Запускаем Mnemora";
        Details = "Подготавливаем приложение";
        IsRunning = true;

        try
        {
            var progress = new InlineProgress<StartupProgress>(ApplyProgress);
            StartupResult result = await startupService.InitializeAsync(progress, _cancellationTokenSource.Token);
            Result = result;

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
        return CanRetry ? RunAsync() : Task.CompletedTask;
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

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
