using CSharpFunctionalExtensions;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Decorators;

public sealed class CommandValidationDecorator<TResponse, TCommand>(
    ValidationExecutor<TCommand> validationExecutor,
    ICommandHandler<TResponse, TCommand> inner)
    : ICommandHandler<TResponse, TCommand>
    where TCommand : ICommandValidation
{
    public Task<Result<TResponse, Errors>> Handle(
        TCommand command,
        CancellationToken cancellationToken)
    {
        return validationExecutor.ExecuteAsync(
            command,
            "Команда",
            token => inner.Handle(command, token),
            cancellationToken);
    }
}