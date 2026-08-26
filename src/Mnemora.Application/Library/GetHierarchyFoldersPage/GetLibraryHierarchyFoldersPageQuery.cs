using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetHierarchyFoldersPage;

public sealed record GetLibraryHierarchyFoldersPageQuery(
    Guid ContainerId,
    int Offset,
    int PageSize)
    : IQueryValidation;
