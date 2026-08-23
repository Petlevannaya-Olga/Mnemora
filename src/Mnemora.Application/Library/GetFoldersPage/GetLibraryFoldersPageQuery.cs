using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetFoldersPage;

public enum LibraryFolderSort
{
    Custom,
    Name,
}

public sealed record GetLibraryFoldersPageQuery(
    Guid ContainerId,
    string? Search,
    LibraryFolderSort Sort,
    int Offset,
    int PageSize)
    : IQueryValidation;
