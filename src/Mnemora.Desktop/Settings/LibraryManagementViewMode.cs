using System.Text.Json.Serialization;
namespace Mnemora.Desktop.Settings;

#pragma warning disable CA1707
public enum LibraryManagementViewMode
{
    [JsonStringEnumMemberName("Table")]
    TABLE,
    [JsonStringEnumMemberName("Tiles")]
    TILES,
    [JsonStringEnumMemberName("CompactTiles")]
    COMPACT_TILES
}
#pragma warning restore CA1707