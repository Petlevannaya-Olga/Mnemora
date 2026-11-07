using System.Text.Json.Serialization;
namespace Mnemora.Desktop.Settings;

#pragma warning disable CA1707
public enum LibraryFoldersViewMode
{
    [JsonStringEnumMemberName("Tiles")]
    TILES,
    [JsonStringEnumMemberName("CompactTiles")]
    COMPACT_TILES,
    [JsonStringEnumMemberName("Table")]
    TABLE
}
#pragma warning restore CA1707