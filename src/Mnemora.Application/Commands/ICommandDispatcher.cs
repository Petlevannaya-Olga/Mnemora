using CSharpFunctionalExtensions;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Commands;

public interface ICommandDispatcher
{
    Task<Result<TResponse, Errors>> SendAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand;

    Task<UnitResult<Errors>> SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand;
}