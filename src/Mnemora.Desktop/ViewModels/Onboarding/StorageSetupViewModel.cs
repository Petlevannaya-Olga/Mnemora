using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed partial class StorageSetupViewModel(INavigationService navigationService) : ViewModelBase
{
    public string Title => "Настройка хранилища";

    public string Description => "Здесь будет выбрана папка для материалов Mnemora.";
    
    public string? StoragePath { get; }

    public string? StorageStatusTitle { get; }

    public string? StorageStatusMessage { get; }

    [RelayCommand]
    private void Back()
    {
        navigationService.NavigateTo<ProfileSetupViewModel>();
    }
    
    [RelayCommand]
    private void SelectFolder()
    {
    }

    [RelayCommand]
    private void Continue()
    {
    }
}