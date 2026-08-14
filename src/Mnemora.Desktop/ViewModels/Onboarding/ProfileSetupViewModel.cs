using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Settings;
using System.Windows;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class ProfileSetupViewModel(
    ISettingsService settingsService,
    INavigationService navigationService,
    OnboardingState onboardingState)
    : ViewModelBase
{
    private string? _name = onboardingState.UserName;
    private string? _saveError;

    public string? Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
            {
                return;
            }

            onboardingState.UserName = value;
            _saveError = null;

            OnPropertyChanged(nameof(Initial));
            OnPropertyChanged(nameof(IsNameValid));
            OnPropertyChanged(nameof(IsNameInvalid));
            OnPropertyChanged(nameof(ValidationMessage));

            ContinueCommand.NotifyCanExecuteChanged();
        }
    }

    public string Initial =>
        string.IsNullOrWhiteSpace(Name)
            ? string.Empty
            : Name.Trim()[0].ToString().ToUpperInvariant();

    public bool IsNameValid =>
        ValidateName(Name) is null;

    public bool IsNameInvalid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !IsNameValid;

    public string? ValidationMessage =>
        _saveError ??
        (string.IsNullOrWhiteSpace(Name)
            ? null
            : ValidateName(Name));

    [RelayCommand]
    private static void Exit()
    {
        System.Windows.Application.Current.Shutdown();
    }

    private bool CanContinue()
    {
        return IsNameValid;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        string? validationMessage = ValidateName(Name);

        if (validationMessage is not null)
        {
            OnPropertyChanged(nameof(ValidationMessage));
            return;
        }

        try
        {
            string userName = Name!.Trim();

            onboardingState.UserName = userName;

            await settingsService.SaveUserNameAsync(userName);

            navigationService.NavigateTo<StorageSetupViewModel>();
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or JsonException)
        {
            _saveError =
                "Не удалось сохранить имя. Попробуйте ещё раз.";

            OnPropertyChanged(nameof(ValidationMessage));
        }
    }

    private static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Введите имя";
        }

        string trimmedName = name.Trim();

        switch (trimmedName.Length)
        {
            case < 2:
                return "Имя должно содержать не менее 2 символов";
            case > 50:
                return "Имя должно содержать не более 50 символов";
            default:
                {
                    bool hasInvalidCharacters = trimmedName.Any(character =>
                        !char.IsLetter(character) &&
                        character != ' ' &&
                        character != '-' &&
                        character != '\'' &&
                        character != '’');

                    return hasInvalidCharacters
                        ? "Допустимы только буквы, пробел, дефис и апостроф"
                        : null;
                }
        }
    }
}