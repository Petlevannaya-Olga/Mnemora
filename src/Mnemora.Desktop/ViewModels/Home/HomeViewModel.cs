using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Sections;

namespace Mnemora.Desktop.ViewModels.Home;

public sealed partial class HomeViewModel
    : ViewModelBase
{
    private readonly IFolderLauncherService _folderLauncherService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private string? _storageErrorMessage;

    public HomeViewModel(
        OnboardingState onboardingState,
        TimeProvider timeProvider,
        IFolderLauncherService folderLauncherService,
        IDialogService dialogService,
        INavigationService navigationService)
    {
        _folderLauncherService = folderLauncherService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        string greeting =
            GetGreeting(
                timeProvider.GetLocalNow().Hour);

        string? userName =
            string.IsNullOrWhiteSpace(
                onboardingState.UserName)
                ? null
                : onboardingState.UserName.Trim();

        Greeting = userName is null
            ? greeting
            : $"{greeting}, {userName}";

        StoragePath =
            string.IsNullOrWhiteSpace(
                onboardingState.StoragePath)
                ? null
                : onboardingState.StoragePath.Trim();
    }

    public string Greeting { get; }

    public string? StoragePath { get; }

    public string StoragePathDisplay =>
        StoragePath ??
        "Путь к хранилищу не выбран";

    public bool IsLibraryEmpty => true;

    public int ExperiencePoints => 0;

    public int Level => 1;

    public int NextLevel =>
        Level + 1;

    public int CurrentLevelExperience => 0;

    public int ExperienceForNextLevel => 100;

    public int StreakDays => 0;

    public string AnswerSummary =>
        "Нет ответов";

    public string? StorageErrorMessage
    {
        get => _storageErrorMessage;

        private set
        {
            if (!SetProperty(
                    ref _storageErrorMessage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasStorageError));
        }
    }

    public bool HasStorageError =>
        !string.IsNullOrWhiteSpace(
            StorageErrorMessage);

    private bool CanOpenStorageFolder()
    {
        return !string.IsNullOrWhiteSpace(
            StoragePath);
    }

    [RelayCommand(CanExecute = nameof(CanOpenStorageFolder))]
    private void OpenStorageFolder()
    {
        StorageErrorMessage = null;

        try
        {
            _folderLauncherService.Open(StoragePath!);
        }
        catch (DirectoryNotFoundException)
        {
            StorageErrorMessage = "Папка хранилища больше не существует. Выберите другую папку в настройках.";
        }
        catch (Exception exception)
            when (exception is Win32Exception
                      or UnauthorizedAccessException
                      or InvalidOperationException
                      or NotSupportedException
                      or ArgumentException)
        {
            StorageErrorMessage = "Не удалось открыть папку хранилища.";
        }
    }
    
    [RelayCommand]
    private void CreateFirstSection()
    {
        var sectionId = _dialogService.Show<
            CreateSectionDialogViewModel,
            Guid?>();

        if (sectionId is null)
        {
            return;
        }

        _navigationService.NavigateTo<LibraryViewModel>();
    }

    private static string GetGreeting(
        int hour)
    {
        return hour switch
        {
            >= 5 and < 12 => "Доброе утро",
            >= 12 and < 18 => "Добрый день",
            >= 18 and < 23 => "Добрый вечер",
            _ => "Доброй ночи",
        };
    }
}