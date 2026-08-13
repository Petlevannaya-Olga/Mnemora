using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class WelcomeViewModel(INavigationService navigationService) : ViewModelBase
{
    [RelayCommand]
    private void Start()
    {
        navigationService.NavigateTo<ProfileSetupViewModel>();
    }
}