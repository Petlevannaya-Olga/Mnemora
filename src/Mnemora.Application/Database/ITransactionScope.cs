using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Application.Database;

public interface ITransactionScope : IAsyncDisposable
{
    Task<UnitResult<Error>> CommitAsync(CancellationToken cancellationToken);

    Task<UnitResult<Error>> RollbackAsync(CancellationToken cancellationToken);
}