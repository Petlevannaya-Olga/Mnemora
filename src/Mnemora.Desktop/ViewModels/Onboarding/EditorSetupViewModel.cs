using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class EditorSetupViewModel : ViewModelBase
{
    private readonly IFolderPickerService _folderPickerService;
    private readonly IMarkdownEditorService _markdownEditorService;
    private readonly ISettingsService _settingsService;
    private readonly INavigationService _navigationService;
    private readonly OnboardingState _onboardingState;

    private MarkdownEditorType? _selectedEditor;
    private string? _visualStudioCodePath;
    private string? _obsidianVaultPath;
    private bool _isVisualStudioCodeInstalled;
    private bool _isObsidianInstalled;
    private bool _isVisualStudioCodeVerified;
    private bool _isObsidianVerified;
    private bool _isConfigurationInvalid;
    private bool _isChecking;
    private string _configurationTitle = "Редактор не выбран";
    private string _configurationMessage =
        "Выберите Visual Studio Code или Obsidian";

    public EditorSetupViewModel(
        IFolderPickerService folderPickerService,
        IMarkdownEditorService markdownEditorService,
        ISettingsService settingsService,
        INavigationService navigationService,
        OnboardingState onboardingState)
    {
        _folderPickerService = folderPickerService;
        _markdownEditorService = markdownEditorService;
        _settingsService = settingsService;
        _navigationService = navigationService;
        _onboardingState = onboardingState;

        _selectedEditor = onboardingState.MarkdownEditor;
        _visualStudioCodePath = onboardingState.VisualStudioCodePath;

        // Для Obsidian отдельный путь больше не выбирается:
        // Vault всегда совпадает с хранилищем Mnemora.
        _obsidianVaultPath = onboardingState.StoragePath;

        string? detectedVisualStudioCode =
            markdownEditorService.FindVisualStudioCodeExecutable();

        if (!IsValidVisualStudioCodePath(_visualStudioCodePath))
        {
            _visualStudioCodePath = detectedVisualStudioCode;
        }

        _isVisualStudioCodeInstalled =
            IsValidVisualStudioCodePath(_visualStudioCodePath);

        _isObsidianInstalled =
            markdownEditorService.IsObsidianInstalled();

        if (_selectedEditor is null)
        {
            if (_isVisualStudioCodeInstalled)
            {
                _selectedEditor =
                    MarkdownEditorType.VisualStudioCode;
            }
            else if (_isObsidianInstalled)
            {
                _selectedEditor =
                    MarkdownEditorType.Obsidian;
            }
        }

        _onboardingState.MarkdownEditor = _selectedEditor;
        _onboardingState.VisualStudioCodePath = _visualStudioCodePath;
        _onboardingState.ObsidianVaultPath = _obsidianVaultPath;

        _isVisualStudioCodeVerified =
            onboardingState.IsVisualStudioCodeVerified &&
            IsVisualStudioCodeConfigurationReady();

        _isObsidianVerified =
            onboardingState.IsObsidianVerified &&
            IsObsidianConfigurationReady();

        onboardingState.IsVisualStudioCodeVerified =
            _isVisualStudioCodeVerified;
        onboardingState.IsObsidianVerified =
            _isObsidianVerified;

        RefreshConfigurationState();
    }

    public MarkdownEditorType? SelectedEditor
    {
        get => _selectedEditor;
        private set
        {
            if (!SetProperty(ref _selectedEditor, value))
            {
                return;
            }

            _onboardingState.MarkdownEditor = value;

            IsConfigurationInvalid = false;

            OnPropertyChanged(nameof(IsVisualStudioCodeSelected));
            OnPropertyChanged(nameof(IsObsidianSelected));
            OnPropertyChanged(nameof(HasSelectedEditor));
            OnPropertyChanged(nameof(IsConfigurationVerified));

            ContinueCommand.NotifyCanExecuteChanged();
            CheckConfigurationCommand.NotifyCanExecuteChanged();

            RefreshConfigurationState();
        }
    }

    public string? VisualStudioCodePath
    {
        get => _visualStudioCodePath;
        private set
        {
            if (!SetProperty(ref _visualStudioCodePath, value))
            {
                return;
            }

            _onboardingState.VisualStudioCodePath = value;

            bool isInstalled =
                IsValidVisualStudioCodePath(value);

            if (_isVisualStudioCodeInstalled != isInstalled)
            {
                _isVisualStudioCodeInstalled = isInstalled;
                OnPropertyChanged(nameof(IsVisualStudioCodeInstalled));
                OnPropertyChanged(nameof(VisualStudioCodeAvailabilityText));
            }

            ResetVisualStudioCodeVerification();
            RefreshConfigurationState();
        }
    }

    public string? StoragePath =>
        _onboardingState.StoragePath;

    public string? ObsidianVaultPath =>
        _obsidianVaultPath;

    public bool IsVisualStudioCodeInstalled =>
        _isVisualStudioCodeInstalled;

    public bool IsObsidianInstalled
    {
        get => _isObsidianInstalled;
        private set
        {
            if (!SetProperty(ref _isObsidianInstalled, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ObsidianAvailabilityText));

            ResetObsidianVerification();
            RefreshConfigurationState();
        }
    }

    public string VisualStudioCodeAvailabilityText =>
        IsVisualStudioCodeInstalled
            ? "Установлен"
            : "Не найден";

    public string ObsidianAvailabilityText =>
        IsObsidianInstalled
            ? "Установлен"
            : "Не найден";

    public bool IsVisualStudioCodeSelected =>
        SelectedEditor ==
        MarkdownEditorType.VisualStudioCode;

    public bool IsObsidianSelected =>
        SelectedEditor ==
        MarkdownEditorType.Obsidian;

    public bool HasSelectedEditor =>
        SelectedEditor is not null;

    public bool IsConfigurationValid =>
        IsConfigurationReady();

    public bool IsVisualStudioCodeVerified =>
        _isVisualStudioCodeVerified;

    public bool IsObsidianVerified =>
        _isObsidianVerified;

    public bool IsConfigurationVerified =>
        SelectedEditor switch
        {
            MarkdownEditorType.VisualStudioCode =>
                IsVisualStudioCodeVerified,

            MarkdownEditorType.Obsidian =>
                IsObsidianVerified,

            _ => false,
        };

    public bool IsConfigurationInvalid
    {
        get => _isConfigurationInvalid;
        private set => SetProperty(
            ref _isConfigurationInvalid,
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

            CheckConfigurationCommand.NotifyCanExecuteChanged();
            ContinueCommand.NotifyCanExecuteChanged();
            RefreshEditorsCommand.NotifyCanExecuteChanged();
        }
    }

    public string ConfigurationTitle
    {
        get => _configurationTitle;
        private set => SetProperty(
            ref _configurationTitle,
            value);
    }

    public string ConfigurationMessage
    {
        get => _configurationMessage;
        private set => SetProperty(
            ref _configurationMessage,
            value);
    }

    [RelayCommand]
    private void SelectVisualStudioCode()
    {
        SelectedEditor =
            MarkdownEditorType.VisualStudioCode;
    }

    [RelayCommand]
    private void SelectObsidian()
    {
        SelectedEditor =
            MarkdownEditorType.Obsidian;
    }

    [RelayCommand]
    private void SelectVisualStudioCodeFolder()
    {
        string? currentFolder =
            GetVisualStudioCodeDirectory(
                VisualStudioCodePath);

        string? selectedFolder =
            _folderPickerService.SelectFolder(
                currentFolder);

        if (selectedFolder is null)
        {
            return;
        }

        string? executable =
            ResolveVisualStudioCodeExecutable(
                selectedFolder);

        if (executable is null)
        {
            VisualStudioCodePath = null;
            ShowConfigurationError(
                "Visual Studio Code не найден",
                "В выбранной папке нет Code.exe");
            return;
        }

        VisualStudioCodePath = executable;
    }

    [RelayCommand]
    private void InstallVisualStudioCode()
    {
        SelectedEditor =
            MarkdownEditorType.VisualStudioCode;

        MarkdownEditorLaunchResult result =
            _markdownEditorService.OpenDownloadPage(
                MarkdownEditorType.VisualStudioCode);

        if (!result.IsSuccess)
        {
            ShowConfigurationError(
                "Не удалось открыть страницу установки",
                result.Message);
            return;
        }

        IsConfigurationInvalid = false;
        ConfigurationTitle =
            "Установите Visual Studio Code";
        ConfigurationMessage =
            "После установки вернитесь в Mnemora и нажмите «Найти снова»";
    }

    [RelayCommand]
    private void InstallObsidian()
    {
        SelectedEditor =
            MarkdownEditorType.Obsidian;

        MarkdownEditorLaunchResult result =
            _markdownEditorService.OpenDownloadPage(
                MarkdownEditorType.Obsidian);

        if (!result.IsSuccess)
        {
            ShowConfigurationError(
                "Не удалось открыть страницу установки",
                result.Message);
            return;
        }

        IsConfigurationInvalid = false;
        ConfigurationTitle =
            "Установите Obsidian";
        ConfigurationMessage =
            "После установки запустите Obsidian один раз, затем вернитесь в Mnemora и нажмите «Найти снова»";
    }

    private bool CanRefreshEditors() =>
        !IsChecking;

    [RelayCommand(CanExecute = nameof(CanRefreshEditors))]
    private void RefreshEditors()
    {
        string? detectedVisualStudioCode =
            _markdownEditorService
                .FindVisualStudioCodeExecutable();

        if (detectedVisualStudioCode is not null)
        {
            VisualStudioCodePath =
                detectedVisualStudioCode;
        }
        else if (!IsValidVisualStudioCodePath(
                     VisualStudioCodePath))
        {
            VisualStudioCodePath = null;
        }

        bool isVisualStudioCodeInstalled =
            IsValidVisualStudioCodePath(
                VisualStudioCodePath);

        if (_isVisualStudioCodeInstalled !=
            isVisualStudioCodeInstalled)
        {
            _isVisualStudioCodeInstalled =
                isVisualStudioCodeInstalled;
            OnPropertyChanged(
                nameof(IsVisualStudioCodeInstalled));
            OnPropertyChanged(
                nameof(VisualStudioCodeAvailabilityText));
        }

        IsObsidianInstalled =
            _markdownEditorService
                .IsObsidianInstalled();

        if (SelectedEditor is null)
        {
            if (IsVisualStudioCodeInstalled)
            {
                SelectedEditor =
                    MarkdownEditorType.VisualStudioCode;
            }
            else if (IsObsidianInstalled)
            {
                SelectedEditor =
                    MarkdownEditorType.Obsidian;
            }
        }

        RefreshConfigurationState();
    }

    private bool CanCheckConfiguration()
    {
        return !IsChecking &&
               IsConfigurationReady();
    }

    [RelayCommand(CanExecute = nameof(CanCheckConfiguration))]
    private async Task CheckConfigurationAsync(
        CancellationToken cancellationToken)
    {
        if (!CanCheckConfiguration() ||
            SelectedEditor is not { } editor)
        {
            return;
        }

        IsChecking = true;
        SetEditorVerification(editor, false);
        IsConfigurationInvalid = false;
        ConfigurationTitle = "Проверяем редактор";
        ConfigurationMessage =
            "Создаём тестовый Markdown-файл и открываем его";

        try
        {
            MarkdownEditorLaunchResult result =
                await _markdownEditorService.CheckAsync(
                    editor,
                    VisualStudioCodePath,
                    ObsidianVaultPath,
                    cancellationToken);

            if (!result.IsSuccess)
            {
                ShowConfigurationError(
                    "Не удалось открыть файл",
                    result.Message);
                return;
            }

            SetEditorVerification(editor, true);
            IsConfigurationInvalid = false;
            ConfigurationTitle = "Редактор готов";
            ConfigurationMessage = result.Message;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            RefreshConfigurationState();
        }
        finally
        {
            IsChecking = false;
        }
    }

    private bool CanContinue()
    {
        return !IsChecking &&
               IsConfigurationVerified &&
               IsConfigurationReady();
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync(
        CancellationToken cancellationToken)
    {
        if (!CanContinue() ||
            SelectedEditor is null)
        {
            return;
        }

        try
        {
            _onboardingState.MarkdownEditor =
                SelectedEditor;
            _onboardingState.VisualStudioCodePath =
                VisualStudioCodePath;
            _onboardingState.ObsidianVaultPath =
                ObsidianVaultPath;

            await _settingsService.SaveMarkdownEditorAsync(
                SelectedEditor,
                VisualStudioCodePath,
                ObsidianVaultPath,
                cancellationToken);

            _navigationService
                .NavigateTo<AiSetupViewModel>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // ignore
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or JsonException
                      or NotSupportedException
                      or ArgumentException)
        {
            ShowConfigurationError(
                "Не удалось сохранить настройку",
                "Проверьте доступ к файлам Mnemora и попробуйте ещё раз");
        }
    }

    [RelayCommand]
    private void Back()
    {
        _onboardingState.MarkdownEditor = SelectedEditor;
        _onboardingState.VisualStudioCodePath = VisualStudioCodePath;
        _onboardingState.ObsidianVaultPath = ObsidianVaultPath;

        _navigationService
            .NavigateTo<StorageSetupViewModel>();
    }

    private bool IsConfigurationReady()
    {
        return SelectedEditor switch
        {
            MarkdownEditorType.VisualStudioCode =>
                IsVisualStudioCodeConfigurationReady(),

            MarkdownEditorType.Obsidian =>
                IsObsidianConfigurationReady(),

            _ => false,
        };
    }

    private bool IsVisualStudioCodeConfigurationReady()
    {
        return IsVisualStudioCodeInstalled &&
               IsValidVisualStudioCodePath(
                   VisualStudioCodePath);
    }

    private bool IsObsidianConfigurationReady()
    {
        return IsObsidianInstalled &&
               IsValidObsidianVault(
                   ObsidianVaultPath);
    }

    private void SetEditorVerification(
        MarkdownEditorType editor,
        bool isVerified)
    {
        switch (editor)
        {
            case MarkdownEditorType.VisualStudioCode:
                SetVisualStudioCodeVerification(
                    isVerified);
                break;

            case MarkdownEditorType.Obsidian:
                SetObsidianVerification(
                    isVerified);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(editor),
                    editor,
                    null);
        }
    }

    private void SetVisualStudioCodeVerification(
        bool isVerified)
    {
        if (_isVisualStudioCodeVerified == isVerified)
        {
            return;
        }

        _isVisualStudioCodeVerified = isVerified;
        _onboardingState.IsVisualStudioCodeVerified =
            isVerified;

        OnPropertyChanged(
            nameof(IsVisualStudioCodeVerified));

        if (IsVisualStudioCodeSelected)
        {
            OnPropertyChanged(
                nameof(IsConfigurationVerified));
        }

        ContinueCommand.NotifyCanExecuteChanged();
        CheckConfigurationCommand.NotifyCanExecuteChanged();
    }

    private void SetObsidianVerification(
        bool isVerified)
    {
        if (_isObsidianVerified == isVerified)
        {
            return;
        }

        _isObsidianVerified = isVerified;
        _onboardingState.IsObsidianVerified =
            isVerified;

        OnPropertyChanged(
            nameof(IsObsidianVerified));

        if (IsObsidianSelected)
        {
            OnPropertyChanged(
                nameof(IsConfigurationVerified));
        }

        ContinueCommand.NotifyCanExecuteChanged();
        CheckConfigurationCommand.NotifyCanExecuteChanged();
    }

    private void ResetVisualStudioCodeVerification()
    {
        SetVisualStudioCodeVerification(false);
    }

    private void ResetObsidianVerification()
    {
        SetObsidianVerification(false);
    }

    private void RefreshConfigurationState()
    {
        OnPropertyChanged(nameof(IsConfigurationValid));

        ContinueCommand.NotifyCanExecuteChanged();
        CheckConfigurationCommand.NotifyCanExecuteChanged();

        if (IsConfigurationVerified &&
            IsConfigurationReady())
        {
            IsConfigurationInvalid = false;
            ConfigurationTitle = "Редактор готов";
            ConfigurationMessage = SelectedEditor switch
            {
                MarkdownEditorType.VisualStudioCode =>
                    "Mnemora может открывать Markdown-файлы в Visual Studio Code",
                MarkdownEditorType.Obsidian =>
                    "Mnemora может открывать Markdown-файлы в Obsidian",
                _ => string.Empty,
            };
            return;
        }

        IsConfigurationInvalid = false;

        switch (SelectedEditor)
        {
            case MarkdownEditorType.VisualStudioCode:
                if (!IsVisualStudioCodeInstalled)
                {
                    ConfigurationTitle =
                        "Visual Studio Code не найден";
                    ConfigurationMessage =
                        "Установите редактор или укажите папку с Code.exe";
                }
                else
                {
                    ConfigurationTitle =
                        "Редактор не проверен";
                    ConfigurationMessage =
                        "Проверьте, что Mnemora может открыть Markdown-файл";
                }
                break;

            case MarkdownEditorType.Obsidian:
                if (!IsObsidianInstalled)
                {
                    ConfigurationTitle =
                        "Obsidian не найден";
                    ConfigurationMessage =
                        "Установите приложение и нажмите «Найти снова»";
                }
                else if (!IsValidObsidianVault(
                             ObsidianVaultPath))
                {
                    ConfigurationTitle =
                        "Подключите папку Mnemora к Obsidian";
                    ConfigurationMessage =
                        "Откройте указанную папку в Obsidian через «Открыть папку как Vault», " +
                        "затем нажмите «Найти снова»";
                }
                else
                {
                    ConfigurationTitle =
                        "Редактор не проверен";
                    ConfigurationMessage =
                        "Проверьте, что Mnemora может открыть Markdown-файл";
                }
                break;

            default:
                ConfigurationTitle =
                    "Редактор не выбран";
                ConfigurationMessage =
                    "Выберите Visual Studio Code или Obsidian";
                break;
        }
    }

    private void ShowConfigurationError(
        string title,
        string message)
    {
        if (SelectedEditor is { } editor)
        {
            SetEditorVerification(
                editor,
                false);
        }

        IsConfigurationInvalid = true;
        ConfigurationTitle = title;
        ConfigurationMessage = message;
    }

    private static bool IsValidVisualStudioCodePath(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return File.Exists(path) &&
                   string.Equals(
                       Path.GetFileName(path),
                       "Code.exe",
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
            when (exception is ArgumentException
                      or NotSupportedException
                      or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsValidObsidianVault(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            string fullPath =
                Path.GetFullPath(path);

            return Directory.Exists(fullPath) &&
                   Directory.Exists(
                       Path.Combine(
                           fullPath,
                           ".obsidian"));
        }
        catch (Exception exception)
            when (exception is ArgumentException
                      or NotSupportedException
                      or PathTooLongException
                      or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? ResolveVisualStudioCodeExecutable(
        string selectedFolder)
    {
        string fullPath =
            Path.GetFullPath(selectedFolder);

        string[] candidates =
        [
            Path.Combine(
                fullPath,
                "Code.exe"),
            Path.Combine(
                fullPath,
                "Microsoft VS Code",
                "Code.exe"),
            Path.Combine(
                fullPath,
                "..",
                "Code.exe"),
        ];

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }

    private static string? GetVisualStudioCodeDirectory(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetDirectoryName(path);
        }
        catch (Exception exception)
            when (exception is ArgumentException
                      or NotSupportedException
                      or PathTooLongException)
        {
            return null;
        }
    }
}
