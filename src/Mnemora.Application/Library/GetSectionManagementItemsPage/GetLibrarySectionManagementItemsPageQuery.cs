using System.Text.Json.Serialization;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetSectionManagementItemsPage;

#pragma warning disable CA1707
public enum LibrarySectionManagementItemSort
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

public enum LibrarySectionManagementItemFilter
{
    [JsonStringEnumMemberName("All")]
    ALL,
    [JsonStringEnumMemberName("Folders")]
    FOLDERS,
    [JsonStringEnumMemberName("Articles")]
    ARTICLES,
    [JsonStringEnumMemberName("Questions")]
    QUESTIONS,
}

public sealed record GetLibrarySectionManagementItemsPageQuery(
    Guid SectionId,
    string? Search,
    LibrarySectionManagementItemFilter Filter,
    LibrarySectionManagementItemSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
