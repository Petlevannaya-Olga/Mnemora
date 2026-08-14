using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Queries;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Desktop.Queries;

internal sealed class QueryDispatcher(
    IServiceScopeFactory scopeFactory)
    : IQueryDispatcher
{
    public async Task<Result<TResponse, Errors>> SendAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<IQueryHandler<TResponse, TQuery>>();

        return await handler.Handle(query, cancellationToken);
    }
}