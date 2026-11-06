using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;
using Xunit;

namespace Mnemora.Desktop.Tests.Navigation;

public sealed class PageNavigationServiceTests
{
    [Fact]
    public void NavigateBack_RestoresPreviousViewModelInstance()
    {
        var services = new ServiceCollection();
        services.AddTransient<FirstPageViewModel>();
        services.AddTransient<SecondPageViewModel>();

        using ServiceProvider provider = services.BuildServiceProvider();
        var navigation = new PageNavigationService(provider);

        navigation.NavigateTo<FirstPageViewModel>();
        ViewModelBase firstPage = navigation.CurrentPageViewModel!;

        navigation.NavigateTo<SecondPageViewModel>();
        navigation.CanNavigateBack.Should().BeTrue();

        navigation.NavigateBack();

        navigation.CurrentPageViewModel.Should().BeSameAs(firstPage);
    }

    private sealed class FirstPageViewModel : ViewModelBase;

    private sealed class SecondPageViewModel : ViewModelBase;
}
