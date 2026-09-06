using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetSectionManagementItemsPage;

public enum LibrarySectionManagementItemSort
{
    Custom,
    RecentActivity,
    Name,
    Newest,
}

public sealed record GetLibrarySectionManagementItemsPageQuery(
    Guid SectionId,
    string? Search,
    LibrarySectionManagementItemSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
