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
    Guid ContainerId,
    string? Search,
    LibraryManagementMaterialPageFilter Filter,
    LibraryManagementMaterialPageSort Sort,
    int Offset,
    int PageSize)
    : IQuery
{
    // Переходный alias для старого UI-кода. Удалим вместе с Topic-моделью.
    public Guid TopicId => ContainerId;
}
