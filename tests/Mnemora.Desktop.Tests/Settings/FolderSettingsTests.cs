using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Mnemora.Desktop.Settings;
using Xunit;

namespace Mnemora.Desktop.Tests.Settings;

public sealed class FolderSettingsTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void CurrentFolderSettings_RoundTrip()
    {
        Guid sectionId = Guid.NewGuid();
        AppSettings original = new()
        {
            LibraryFoldersViewMode = LibraryFoldersViewMode.Table,
            LibraryManagementFoldersViewMode = LibraryManagementViewMode.CompactTiles,
            LibraryManagementFolderSortBySection = new()
            {
                [sectionId] = LibraryManagementSortMode.Newest,
            },
        };

        string json = JsonSerializer.Serialize(original, Options);
        AppSettings restored = JsonSerializer.Deserialize<AppSettings>(json, Options)!;

        restored.LibraryFoldersViewMode.Should().Be(LibraryFoldersViewMode.Table);
        restored.LibraryManagementFoldersViewMode.Should().Be(LibraryManagementViewMode.CompactTiles);
        restored.LibraryManagementFolderSortBySection[sectionId].Should().Be(LibraryManagementSortMode.Newest);
    }

    [Fact]
    public void DefaultSettings_DoNotRequireAnyMigration()
    {
        AppSettings settings = JsonSerializer.Deserialize<AppSettings>("{}", Options)!;
        settings.LibraryOverviewViewMode.Should().Be(LibraryOverviewViewMode.Tiles);
        settings.LibraryFoldersViewMode.Should().Be(LibraryFoldersViewMode.CompactTiles);
        settings.LibraryManagementMaterialsViewMode.Should().Be(LibraryManagementViewMode.Table);
    }
}
