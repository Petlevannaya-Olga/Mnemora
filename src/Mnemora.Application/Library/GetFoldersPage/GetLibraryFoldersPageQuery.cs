using System.Text.Json.Serialization;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetFoldersPage;

#pragma warning disable CA1707
public enum LibraryFolderSort
{
    [JsonStringEnumMemberName("Custom")]
    CUSTOM,
    [JsonStringEnumMemberName("RecentlyUpdated")]
    RECENTLY_UPDATED,
    [JsonStringEnumMemberName("Name")]
    NAME,
    [JsonStringEnumMemberName("Newest")]
    NEWEST,
}
#pragma warning restore CA1707

public sealed record GetLibraryFoldersPageQuery(
    Guid ContainerId,
    string? Search,
    LibraryFolderSort Sort,
    int Offset,
    int PageSize)
    : IQueryValidation;
