using System.Text.Json.Serialization;
namespace Mnemora.Application.Library.GetSectionsPage;

#pragma warning disable CA1707
public enum LibrarySectionSort
{
    [JsonStringEnumMemberName("Name")]
    NAME,
    [JsonStringEnumMemberName("Newest")]
    NEWEST,
    [JsonStringEnumMemberName("RecentActivity")]
    RECENT_ACTIVITY
}
#pragma warning restore CA1707