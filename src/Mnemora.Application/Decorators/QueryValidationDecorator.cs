using CSharpFunctionalExtensions;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Decorators;

public sealed class QueryValidationDecorator<TResponse, TQuery>(
    ValidationExecutor<TQuery> validationExecutor,
    IQueryHandler<TResponse, TQuery> inner)
    : IQueryHandler<TResponse, TQuery>
    where TQuery : IQueryValidation
{
    public Task<Result<TResponse, Errors>> Handle(
        TQuery query,
        CancellationToken cancellationToken = default)
    {
        return validationExecutor.ExecuteAsync(
            query,
            "Запрос",
            token => inner.Handle(query, token),
            cancellationToken);
    }
}