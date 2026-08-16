using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetTopicsPage;

public sealed record GetLibraryTopicsPageQuery(
    Guid SectionId,
    string? Search,
    LibraryTopicSort Sort,
    int Offset,
    int PageSize)
    : IQueryValidation;