using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Commands;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Desktop.Commands;

internal sealed class CommandDispatcher(
    IServiceScopeFactory scopeFactory)
    : ICommandDispatcher
{
    public async Task<Result<TResponse, Errors>> SendAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<TResponse, TCommand>>();

        return await handler.Handle(command, cancellationToken);
    }

    public async Task<UnitResult<Errors>> SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<TCommand>>();

        return await handler.Handle(command, cancellationToken);
    }
}