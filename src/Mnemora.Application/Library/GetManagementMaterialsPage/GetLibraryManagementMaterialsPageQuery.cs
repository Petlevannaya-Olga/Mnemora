using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementMaterialsPage;

public enum LibraryManagementMaterialPageFilter
{
    All,
    Articles,
    Questions,
}

public enum LibraryManagementMaterialPageSort
{
    Custom,
    RecentActivity,
    Name,
    Newest,
}

public sealed record GetLibraryManagementMaterialsPageQuery(
    Guid TopicId,
    string? Search,
    LibraryManagementMaterialPageFilter Filter,
    LibraryManagementMaterialPageSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
