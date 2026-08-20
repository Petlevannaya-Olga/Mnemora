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
    public Task<Result<TResponse, Errors>> SendAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery
    {
        return Task.Run(
            async () =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var handler = scope.ServiceProvider
                    .GetRequiredService<IQueryHandler<TResponse, TQuery>>();

                return await handler
                    .Handle(query, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }
}
