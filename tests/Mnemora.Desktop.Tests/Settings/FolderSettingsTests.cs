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
            LibraryFoldersViewMode = LibraryFoldersViewMode.TABLE,
            LibraryManagementFoldersViewMode = LibraryManagementViewMode.COMPACT_TILES,
            LibraryManagementFolderSortBySection = new()
            {
                [sectionId] = LibraryManagementSortMode.NEWEST,
            },
        };

        string json = JsonSerializer.Serialize(original, Options);
        AppSettings restored = JsonSerializer.Deserialize<AppSettings>(json, Options)!;

        restored.LibraryFoldersViewMode.Should().Be(LibraryFoldersViewMode.TABLE);
        restored.LibraryManagementFoldersViewMode.Should().Be(LibraryManagementViewMode.COMPACT_TILES);
        restored.LibraryManagementFolderSortBySection[sectionId].Should().Be(LibraryManagementSortMode.NEWEST);
    }

    [Fact]
    public void DefaultSettings_DoNotRequireAnyMigration()
    {
        AppSettings settings = JsonSerializer.Deserialize<AppSettings>("{}", Options)!;
        settings.LibraryOverviewViewMode.Should().Be(LibraryOverviewViewMode.TILES);
        settings.LibraryFoldersViewMode.Should().Be(LibraryFoldersViewMode.COMPACT_TILES);
        settings.LibraryManagementMaterialsViewMode.Should().Be(LibraryManagementViewMode.TABLE);
    }
}
