using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetMaterialsPage;

public sealed record GetLibraryMaterialsPageQuery(
    Guid TopicId,
    string? Search,
    LibraryMaterialFilter Filter,
    LibraryMaterialSort Sort,
    int Offset,
    int PageSize)
    : IQuery;