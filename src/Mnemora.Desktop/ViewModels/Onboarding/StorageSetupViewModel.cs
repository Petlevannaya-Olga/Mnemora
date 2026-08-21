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
    OnboardingState onboardingState,
    IStorageValidationService storageValidationService)
    : ViewModelBase
{
    private string? _storagePath =
        onboardingState.StoragePath;

    private bool _isStorageValid =
        storageValidationService
            .ValidateCandidate(
                onboardingState.StoragePath)
            .IsValid;

    private string? _storageError =
        storageValidationService
            .ValidateCandidate(
                onboardingState.StoragePath)
            .ErrorMessage;

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
            StorageValidationResult validationResult =
                await storageValidationService.PrepareAsync(
                    StoragePath);

            ApplyValidation(validationResult);

            if (!validationResult.IsValid)
            {
                return;
            }

            string storagePath =
                validationResult.NormalizedPath!;

            await settingsService.SaveStoragePathAsync(
                storagePath);

            onboardingState.StoragePath = storagePath;

            navigationService.NavigateTo<EditorSetupViewModel>();
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
        ApplyValidation(
            storageValidationService
                .ValidateCandidate(path));
    }

    private void ApplyValidation(
        StorageValidationResult validationResult)
    {
        _isStorageValid =
            validationResult.IsValid;

        _storageError =
            validationResult.ErrorMessage;

        OnPropertyChanged(nameof(IsStorageValid));
        OnPropertyChanged(nameof(IsStorageInvalid));
        OnPropertyChanged(nameof(ValidationMessage));

        ContinueCommand.NotifyCanExecuteChanged();
    }
}
