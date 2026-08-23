using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetContainerContents;

public sealed record GetLibraryContainerContentsQuery(
    Guid ContainerId)
    : IQueryValidation;
