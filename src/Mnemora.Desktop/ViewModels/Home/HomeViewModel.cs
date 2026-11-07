using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Home.GetLibrarySummary;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.ViewModels.Folders;
using Mnemora.Shared;

namespace Mnemora.Desktop.ViewModels.Home;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly IFolderLauncherService _folderLauncherService;
    private readonly IDialogService _dialogService;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IPageNavigationService _pageNavigationService;

    private string? _storageErrorMessage;
    private string? _libraryErrorMessage;
    private bool _isLoading;
    private int _sectionsCount;
    private int _foldersCount;
    private HomeSuggestedSectionDto? _suggestedSection;

    public HomeViewModel(
        OnboardingState onboardingState,
        TimeProvider timeProvider,
        IFolderLauncherService folderLauncherService,
        IDialogService dialogService,
        IQueryDispatcher queryDispatcher,
        IPageNavigationService pageNavigationService)
    {
        _folderLauncherService = folderLauncherService;
        _dialogService = dialogService;
        _queryDispatcher = queryDispatcher;
        _pageNavigationService = pageNavigationService;

        var greeting = GetGreeting(timeProvider.GetLocalNow().Hour);

        var userName = string.IsNullOrWhiteSpace(onboardingState.UserName)
            ? null
            : onboardingState.UserName.Trim();

        Greeting = userName is null
            ? greeting
            : $"{greeting}, {userName}";

        StoragePath = string.IsNullOrWhiteSpace(onboardingState.StoragePath)
            ? null
            : onboardingState.StoragePath.Trim();
    }

    public bool HasFolders =>
        HasSections && FoldersCount > 0;

    public HomeSuggestedSectionDto? SuggestedSection =>
        _suggestedSection;

    public string Greeting { get; }

    public string? StoragePath { get; }

    public string StoragePathDisplay =>
        StoragePath ?? "Путь к хранилищу не выбран";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
            {
                return;
            }

            NotifyLibraryStateChanged();
        }
    }

    public string? LibraryErrorMessage
    {
        get => _libraryErrorMessage;
        private set
        {
            if (!SetProperty(ref _libraryErrorMessage, value))
            {
                return;
            }

            NotifyLibraryStateChanged();
        }
    }

    public bool HasLibraryError =>
        !string.IsNullOrWhiteSpace(LibraryErrorMessage);

    public bool IsLibraryEmpty =>
        !IsLoading && !HasLibraryError && SectionsCount == 0;

    public bool HasSections =>
        !IsLoading && !HasLibraryError && SectionsCount > 0;

    public bool HasSectionsWithoutFolders =>
        HasSections && FoldersCount == 0;

    public int SectionsCount => _sectionsCount;

    public int FoldersCount => _foldersCount;

    public string SectionsSummary =>
        $"{SectionsCount} {DeclensionGenerator.Generate(
            SectionsCount,
            "раздел",
            "раздела",
            "разделов")}";

    public int ExperiencePoints => 0;

    public int Level => 1;

    public int NextLevel => Level + 1;

    public int CurrentLevelExperience => 0;

    public int ExperienceForNextLevel => 100;

    public int StreakDays => 0;

    public string AnswerSummary => "Нет ответов";

    public string? StorageErrorMessage
    {
        get => _storageErrorMessage;
        private set
        {
            if (!SetProperty(ref _storageErrorMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStorageError));
        }
    }

    public bool HasStorageError =>
        !string.IsNullOrWhiteSpace(StorageErrorMessage);

    public async Task LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        LibraryErrorMessage = null;

        try
        {
            var query = new GetHomeLibrarySummaryQuery();

            var result = await _queryDispatcher.SendAsync<
                GetHomeLibrarySummaryQuery,
                HomeLibrarySummaryDto>(
                query,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                LibraryErrorMessage = result.Error
                                          .FirstOrDefault()
                                          ?.Message
                                      ?? "Не удалось загрузить состояние библиотеки";

                return;
            }

            _sectionsCount = result.Value.SectionsCount;
            _foldersCount = result.Value.FoldersCount;
            _suggestedSection = result.Value.SuggestedSection;

            NotifyLibraryStateChanged();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Отмена операции - ожидаемое поведение
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task CreateFirstSectionAsync(
        CancellationToken cancellationToken)
    {
        return CreateSectionAsync(cancellationToken);
    }

    [RelayCommand]
    private Task AddSectionAsync(
        CancellationToken cancellationToken)
    {
        return CreateSectionAsync(cancellationToken);
    }

    private async Task CreateSectionAsync(
        CancellationToken cancellationToken)
    {
        var sectionId = _dialogService.Show<
            CreateSectionDialogViewModel,
            Guid?>();

        if (sectionId is null)
        {
            return;
        }

        await LoadAsync(cancellationToken);
    }

    private bool CanOpenStorageFolder()
    {
        return !string.IsNullOrWhiteSpace(StoragePath);
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
            StorageErrorMessage =
                "Папка хранилища больше не существует. Выберите другую папку в настройках.";
        }
        catch (Exception exception)
            when (exception is Win32Exception
                      or UnauthorizedAccessException
                      or InvalidOperationException
                      or NotSupportedException
                      or ArgumentException)
        {
            StorageErrorMessage =
                "Не удалось открыть папку хранилища.";
        }
    }

    [RelayCommand]
    private async Task AddFirstFolderAsync(
        CancellationToken cancellationToken)
    {
        var section = SuggestedSection;

        if (section is null)
        {
            return;
        }

        var folderId = _dialogService.Show<
            CreateFolderDialogViewModel,
            Guid?>(viewModel => viewModel.Initialize(
            section.Id,
            section.Name));

        if (folderId is null)
        {
            return;
        }

        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private void OpenLibrary()
    {
        _pageNavigationService.NavigateTo<LibraryOverviewViewModel>();
    }

    private void NotifyLibraryStateChanged()
    {
        OnPropertyChanged(nameof(HasLibraryError));
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(HasSections));
        OnPropertyChanged(nameof(HasSectionsWithoutFolders));
        OnPropertyChanged(nameof(HasFolders));
        OnPropertyChanged(nameof(SectionsCount));
        OnPropertyChanged(nameof(FoldersCount));
        OnPropertyChanged(nameof(SuggestedSection));
        OnPropertyChanged(nameof(SectionsSummary));
    }

    private static string GetGreeting(int hour)
    {
        return hour switch
        {
            >= 5 and < 12 => "Доброе утро",
            >= 12 and < 18 => "Добрый день",
            >= 18 and < 23 => "Добрый вечер",
            _ => "Доброй ночи"
        };
    }
}
