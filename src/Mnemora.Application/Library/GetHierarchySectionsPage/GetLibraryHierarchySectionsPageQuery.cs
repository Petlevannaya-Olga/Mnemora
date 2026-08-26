using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetHierarchySectionsPage;

public sealed record GetLibraryHierarchySectionsPageQuery(
    int Offset,
    int PageSize)
    : IQueryValidation;
