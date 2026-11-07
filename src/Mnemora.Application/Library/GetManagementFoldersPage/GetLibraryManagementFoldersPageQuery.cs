using System.Text.Json.Serialization;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementFoldersPage;

#pragma warning disable CA1707
public enum LibraryManagementFolderPageSort
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

public sealed record GetLibraryManagementFoldersPageQuery(
    Guid SectionId,
    string? Search,
    LibraryManagementFolderPageSort Sort,
    int Offset,
    int PageSize)
    : IQuery;
