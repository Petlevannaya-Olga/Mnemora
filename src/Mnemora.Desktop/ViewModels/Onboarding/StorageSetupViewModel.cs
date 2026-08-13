using System.IO;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Storage;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class StorageSetupViewModel(
    IFolderPickerService folderPickerService,
    INavigationService navigationService,
    OnboardingState onboardingState)
    : ViewModelBase
{
    private string? _storagePath = onboardingState.StoragePath;

    private bool _isStorageValid;

    public string? StoragePath
    {
        get => _storagePath;
        private set
        {
            if (!SetProperty(ref _storagePath, value))
            {
                return;
            }

            onboardingState.StoragePath = value;

            ValidateStorage(value);
        }
    }

    public bool IsStorageValid => _isStorageValid;

    public bool IsStorageInvalid =>
        !string.IsNullOrWhiteSpace(StoragePath) &&
        !IsStorageValid;

    [RelayCommand]
    private void SelectFolder()
    {
        string? selectedPath = folderPickerService.SelectFolder(StoragePath);

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
    private void Continue()
    {
        if (!IsStorageValid)
        {
            return;
        }

        onboardingState.StoragePath = StoragePath!.Trim();

        //navigationService.NavigateTo<AiSetupViewModel>();
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
                !Directory.EnumerateFileSystemEntries(path).Any();

            bool isMnemoraStorage =
                File.Exists(Path.Combine(path, ".mnemora"));

            if (!isEmpty && !isMnemoraStorage)
            {
                return false;
            }

            return CanWriteToDirectory(path);
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or NotSupportedException)
        {
            return false;
        }
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
                FileShare.None);

            stream.WriteByte(0);
            stream.Flush();
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or NotSupportedException)
        {
            return false;
        }

        try
        {
            File.Delete(testFilePath);

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