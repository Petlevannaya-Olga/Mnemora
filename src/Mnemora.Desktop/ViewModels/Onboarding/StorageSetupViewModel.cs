using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class StorageSetupViewModel(
    IFolderPickerService folderPickerService,
    INavigationService navigationService,
    ISettingsService settingsService,
    OnboardingState onboardingState)
    : ViewModelBase
{
    private const string StorageMarkerFileName = ".mnemora";
    private const int CurrentStorageFormatVersion = 1;

    private string? _storagePath =
        onboardingState.StoragePath;

    private bool _isStorageValid =
        CheckStorage(onboardingState.StoragePath);

    private string? _storageError;

    public string? StoragePath
    {
        get => _storagePath;
        private set
        {
            bool changed =
                SetProperty(ref _storagePath, value);

            _storageError = null;

            OnPropertyChanged(nameof(ValidationMessage));

            if (changed)
            {
                onboardingState.StoragePath = value;
            }

            ValidateStorage(value);
        }
    }

    public bool IsStorageValid =>
        _isStorageValid;

    public bool IsStorageInvalid =>
        !string.IsNullOrWhiteSpace(StoragePath) &&
        !IsStorageValid;

    public string? ValidationMessage =>
        _storageError;

    [RelayCommand]
    private void SelectFolder()
    {
        string? selectedPath =
            folderPickerService.SelectFolder(StoragePath);

        if (selectedPath is null)
        {
            return;
        }

        StoragePath = selectedPath;
    }

    private bool CanContinue()
    {
        return IsStorageValid;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        if (!IsStorageValid ||
            string.IsNullOrWhiteSpace(StoragePath))
        {
            return;
        }

        try
        {
            string storagePath =
                Path.GetFullPath(StoragePath.Trim());

            await EnsureStorageMarkerAsync(storagePath);

            await settingsService.SaveStoragePathAsync(
                storagePath);

            onboardingState.StoragePath = storagePath;

            navigationService.NavigateTo<AiSetupViewModel>();
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or JsonException
                      or NotSupportedException)
        {
            _storageError =
                "Не удалось подготовить хранилище или сохранить его путь.";

            OnPropertyChanged(nameof(ValidationMessage));
        }
    }

    [RelayCommand]
    private void Back()
    {
        navigationService.NavigateTo<ProfileSetupViewModel>();
    }

    private void ValidateStorage(string? path)
    {
        _isStorageValid = CheckStorage(path);

        OnPropertyChanged(nameof(IsStorageValid));
        OnPropertyChanged(nameof(IsStorageInvalid));

        ContinueCommand.NotifyCanExecuteChanged();
    }

    private static bool CheckStorage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            bool isEmpty =
                !Directory
                    .EnumerateFileSystemEntries(path)
                    .Any();

            bool isMnemoraStorage =
                HasValidStorageMarker(path);

            if (!isEmpty && !isMnemoraStorage)
            {
                return false;
            }

            return CanWriteToDirectory(path);
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or JsonException
                      or NotSupportedException)
        {
            return false;
        }
    }
    
    private static bool HasValidStorageMarker(
        string storagePath)
    {
        string markerPath = Path.Combine(
            storagePath,
            StorageMarkerFileName);

        if (!File.Exists(markerPath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(markerPath);
        using JsonDocument document = JsonDocument.Parse(stream);

        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty(
                "formatVersion",
                out JsonElement versionElement))
        {
            return false;
        }

        return versionElement.ValueKind ==
               JsonValueKind.Number
               &&
               versionElement.TryGetInt32(
                   out int formatVersion)
               &&
               formatVersion ==
               CurrentStorageFormatVersion;
    }

    private static async Task EnsureStorageMarkerAsync(string storagePath)
    {
        string markerPath = Path.Combine(
            storagePath,
            StorageMarkerFileName);

        if (File.Exists(markerPath))
        {
            if (!HasValidStorageMarker(storagePath))
            {
                throw new InvalidDataException(
                    "Файл маркера Mnemora повреждён или имеет неподдерживаемый формат.");
            }

            return;
        }

        const string markerContent = "{\"formatVersion\":1}";

        await File.WriteAllTextAsync(
            markerPath,
            markerContent);
    }

    private static bool CanWriteToDirectory(string path)
    {
        string testFilePath = Path.Combine(
            path,
            $".mnemora-write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            using FileStream stream = new(
                testFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.DeleteOnClose);

            stream.WriteByte(0);
            stream.Flush(flushToDisk: true);

            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or NotSupportedException)
        {
            return false;
        }
    }
}