using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class CompletionSetupViewModel(
    INavigationService navigationService,
    OnboardingState onboardingState)
    : ViewModelBase
{
    public string UserName =>
        onboardingState.UserName?.Trim()
        ?? string.Empty;

    public string StorageStatus => "Папка для материалов выбрана";

    public bool IsAiConfigured =>
        onboardingState.IsAiConfigured;

    public bool IsAiSkipped =>
        !IsAiConfigured;

    public string AiStatus =>
        IsAiConfigured
            ? "Подключение установлено"
            : "Не подключён — можно настроить позже";

    [RelayCommand]
    private void Back()
    {
        navigationService.NavigateTo<AiSetupViewModel>();
    }

    [RelayCommand]
    private void OpenMnemora()
    {
        // Переход на главный экран добавим после его создания.
    }
}