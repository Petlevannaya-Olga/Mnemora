using CSharpFunctionalExtensions;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Queries;

public interface IQueryDispatcher
{
    Task<Result<TResponse, Errors>> SendAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery;
}