using System.Text.Json;
using System.Text.Json.Serialization;
using Mnemora.Desktop.Settings;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Desktop.Tests.Settings;

public sealed class EnumStableJsonNamesTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void RenamedEnums_PreserveOldJsonNames()
    {
        Assert.Equal("\"CompactTiles\"", JsonSerializer.Serialize(LibraryFoldersViewMode.COMPACT_TILES, Options));
        Assert.Equal("\"DotNet\"", JsonSerializer.Serialize(SectionIcon.DOT_NET, Options));
        Assert.Equal("\"Bookmark\"", JsonSerializer.Serialize(FolderIcon.BOOKMARK, Options));
        Assert.Equal("\"Teal\"", JsonSerializer.Serialize(SectionColor.TEAL, Options));
        Assert.Equal("\"Teal\"", JsonSerializer.Serialize(FolderColor.TEAL, Options));
    }

    [Fact]
    public void RenamedEnums_ReadOldJsonNames()
    {
        Assert.Equal(LibraryFoldersViewMode.COMPACT_TILES,
            JsonSerializer.Deserialize<LibraryFoldersViewMode>("\"CompactTiles\"", Options));
        Assert.Equal(SectionIcon.DOT_NET,
            JsonSerializer.Deserialize<SectionIcon>("\"DotNet\"", Options));
        Assert.Equal(FolderIcon.BOOKMARK,
            JsonSerializer.Deserialize<FolderIcon>("\"Bookmark\"", Options));
    }
}
