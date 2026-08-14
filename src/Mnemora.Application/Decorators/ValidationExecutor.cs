using CSharpFunctionalExtensions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Decorators;

public sealed class ValidationExecutor<TRequest>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationExecutor<TRequest>> logger)
    where TRequest : IValidation
{
    public async Task<Result<TResponse, Errors>> ExecuteAsync<TResponse>(
        TRequest request,
        string requestKind,
        Func<CancellationToken, Task<Result<TResponse, Errors>>> next,
        CancellationToken cancellationToken)
    {
        var failedResults = new List<ValidationResult>();

        foreach (var validator in validators)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
            {
                failedResults.Add(validationResult);
            }
        }

        if (failedResults.Count == 0)
        {
            return await next(cancellationToken);
        }

        var errors = failedResults.ToErrors();

        logger.LogWarning(
            "{RequestKind} {RequestType} не прошла валидацию: {@Errors}",
            requestKind,
            typeof(TRequest).Name,
            errors);

        return errors;
    }
}