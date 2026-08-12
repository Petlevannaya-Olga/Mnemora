using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Settings;
using System.Windows;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed class ProfileSetupViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    private string? _name;
    private string? _saveError;

    public ProfileSetupViewModel(
        ISettingsService settingsService)
    {
        _settingsService = settingsService;

        ExitCommand = new RelayCommand(Exit);
        ContinueCommand = new AsyncRelayCommand(
            ContinueAsync,
            CanContinue);
    }

    public string? Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
            {
                return;
            }

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

    public IRelayCommand ExitCommand { get; }

    public IAsyncRelayCommand ContinueCommand { get; }

    public event EventHandler? ProfileCompleted;

    private void Exit()
    {
        Application.Current.Shutdown();
    }

    private bool CanContinue()
    {
        return IsNameValid;
    }

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
            await _settingsService.SaveUserNameAsync(Name!.Trim());

            ProfileCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
            when (exception is IOException
                  or UnauthorizedAccessException
                  or JsonException)
        {
            _saveError = "Не удалось сохранить имя. Попробуйте ещё раз.";

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