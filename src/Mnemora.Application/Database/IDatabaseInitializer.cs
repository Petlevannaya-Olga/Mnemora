using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Application.Database;

public interface IDatabaseInitializer
{
    Task<UnitResult<Error>> InitializeAsync(CancellationToken cancellationToken = default);
}