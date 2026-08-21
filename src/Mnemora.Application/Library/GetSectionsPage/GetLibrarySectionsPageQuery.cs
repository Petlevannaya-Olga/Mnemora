using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetSectionsPage;

public sealed record GetLibrarySectionsPageQuery(
    string? Search,
    LibrarySectionSort Sort,
    int Offset,
    int PageSize)
    : IQueryValidation;
