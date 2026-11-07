using System.Text.Json.Serialization;
namespace Mnemora.Application.Library.GetMaterialsPage;

#pragma warning disable CA1707
public enum LibraryMaterialSort
{
    [JsonStringEnumMemberName("Custom")]
    CUSTOM,
    [JsonStringEnumMemberName("RecentlyUpdated")]
    RECENTLY_UPDATED,
    [JsonStringEnumMemberName("Name")]
    NAME,
    [JsonStringEnumMemberName("Newest")]
    NEWEST,
    [JsonStringEnumMemberName("Easiest")]
    EASIEST,
    [JsonStringEnumMemberName("Hardest")]
    HARDEST
}
#pragma warning restore CA1707