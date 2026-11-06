using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetMaterialsPage;

public sealed record GetLibraryMaterialsPageQuery(
    Guid ContainerId,
    string? Search,
    LibraryMaterialFilter Filter,
    LibraryMaterialSort Sort,
    int Offset,
    int PageSize)
    : IQueryValidation
{
}
