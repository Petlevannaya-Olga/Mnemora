using System.Text.Json.Serialization;
namespace Mnemora.Desktop.Settings;

#pragma warning disable CA1707
public enum LibraryManagementSortMode
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
