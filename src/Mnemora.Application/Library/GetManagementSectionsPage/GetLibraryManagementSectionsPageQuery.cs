using System.Text.Json.Serialization;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementSectionsPage;

#pragma warning disable CA1707
public enum LibraryManagementSectionSort
{
    [JsonStringEnumMemberName("Custom")]
    CUSTOM,
    [JsonStringEnumMemberName("RecentActivity")]
    RECENT_ACTIVITY,
    [JsonStringEnumMemberName("Name")]
    NAME,
    [JsonStringEnumMemberName("Newest")]
    NEWEST,
}
#pragma warning restore CA1707

public sealed record GetLibraryManagementSectionsPageQuery(
    string? Search,
    LibraryManagementSectionSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
