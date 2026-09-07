using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetSectionManagementItemsPage;

public enum LibrarySectionManagementItemSort
{
    Custom,
    RecentActivity,
    Name,
    Newest,
}

public enum LibrarySectionManagementItemFilter
{
    All,
    Folders,
    Articles,
    Questions,
}

public sealed record GetLibrarySectionManagementItemsPageQuery(
    Guid SectionId,
    string? Search,
    LibrarySectionManagementItemFilter Filter,
    LibrarySectionManagementItemSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
