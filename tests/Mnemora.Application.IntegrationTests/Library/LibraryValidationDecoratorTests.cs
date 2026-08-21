using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.IntegrationTests.Materials;
using Mnemora.Application.Library.GetSectionsPage;
using Mnemora.Contracts.Library;
using Mnemora.Shared.Abstractions;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class LibraryValidationDecoratorTests
{
    [Fact]
    public async Task InvalidSectionsPageQuery_IsRejectedBeforeHandlerNormalization()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            IQueryHandler<LibrarySectionsPageDto, GetLibrarySectionsPageQuery>>();
        var query = new GetLibrarySectionsPageQuery(
            Search: null,
            LibrarySectionSort.Name,
            Offset: -1,
            PageSize: 1_000);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.InvalidField == nameof(query.Offset));
        Assert.Contains(result.Error, error => error.InvalidField == nameof(query.PageSize));
    }
}
