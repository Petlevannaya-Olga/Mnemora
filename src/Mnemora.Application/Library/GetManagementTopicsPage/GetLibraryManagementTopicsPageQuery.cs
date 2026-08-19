using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementTopicsPage;

public enum LibraryManagementTopicPageSort
{
    Custom,
    RecentActivity,
    Name,
    Newest,
}

public sealed record GetLibraryManagementTopicsPageQuery(
    Guid SectionId,
    string? Search,
    LibraryManagementTopicPageSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
