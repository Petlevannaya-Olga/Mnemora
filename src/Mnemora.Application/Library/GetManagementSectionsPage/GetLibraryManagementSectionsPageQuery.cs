using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementSectionsPage;

public enum LibraryManagementSectionSort
{
    Custom,
    RecentActivity,
    Name,
    Newest,
}

public sealed record GetLibraryManagementSectionsPageQuery(
    string? Search,
    LibraryManagementSectionSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
