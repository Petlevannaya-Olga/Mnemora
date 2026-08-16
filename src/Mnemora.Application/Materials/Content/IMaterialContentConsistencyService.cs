using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Application.Materials.Content;

public interface IMaterialContentConsistencyService
{
    Task<Result<MaterialContentConsistencyReport, Error>> CheckAndRepairAsync(CancellationToken cancellationToken);
}

public sealed record MaterialContentConsistencyReport(
    int QuarantinedDirectoryCount,
    int MissingContentCount,
    int InvalidDirectoryCount);