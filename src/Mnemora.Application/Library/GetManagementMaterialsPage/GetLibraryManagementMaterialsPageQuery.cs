using System.Text.Json.Serialization;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementMaterialsPage;

public enum LibraryManagementMaterialPageFilter
{
    [JsonStringEnumMemberName("All")]
    ALL,
    [JsonStringEnumMemberName("Articles")]
    ARTICLES,
    [JsonStringEnumMemberName("Questions")]
    QUESTIONS,
}

#pragma warning disable CA1707
public enum LibraryManagementMaterialPageSort
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

public sealed record GetLibraryManagementMaterialsPageQuery(
    Guid ContainerId,
    string? Search,
    LibraryManagementMaterialPageFilter Filter,
    LibraryManagementMaterialPageSort Sort,
    int Offset,
    int PageSize)
    : IQuery
{
}
