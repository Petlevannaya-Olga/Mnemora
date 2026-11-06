using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementFoldersPage;

public enum LibraryManagementFolderPageSort
{
    Custom,
    RecentActivity,
    Name,
    Newest,
}

public sealed record GetLibraryManagementFoldersPageQuery(
    Guid SectionId,
    string? Search,
    LibraryManagementFolderPageSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
