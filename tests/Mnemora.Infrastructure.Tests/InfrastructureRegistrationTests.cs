using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Database;
using Mnemora.Application.Materials.Content;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_BuildsValidatedServiceGraph()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path,
            validateOnBuild: true);

        Assert.NotNull(provider.GetRequiredService<IMaterialContentStore>());

        using IServiceScope scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITransactionManager>());
        Assert.Null(scope.ServiceProvider.GetService<ITransactionScope>());
    }
}
