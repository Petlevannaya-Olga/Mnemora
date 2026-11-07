using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Mnemora.Desktop.Settings;
using Xunit;

namespace Mnemora.Desktop.Tests.Settings;

public sealed class FolderSettingsCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void OldTopicKeys_AreReadAndSavedAsFolderKeys()
    {
        Guid sectionId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();

        string json = $$"""
            {
              "libraryManagementTopicsViewMode": "Table",
              "libraryTopicsViewMode": "Tiles",
              "libraryManagementTopicSortBySection": {
                "{{sectionId}}": "Newest"
              },
              "libraryManagementMaterialSortByTopic": {
                "{{folderId}}": "Name"
              }
            }
            """;

        AppSettings settings = ReadSettings(json);

        settings.LibraryManagementFoldersViewMode.Should().Be(LibraryManagementViewMode.Table);
        settings.LibraryFoldersViewMode.Should().Be(LibraryFoldersViewMode.Tiles);
        settings.LibraryManagementFolderSortBySection[sectionId].Should().Be(LibraryManagementSortMode.Newest);
        settings.LibraryManagementMaterialSortByFolder[folderId].Should().Be(LibraryManagementSortMode.Name);

        string savedJson = JsonSerializer.Serialize(settings, JsonOptions);
        savedJson.Should().Contain("libraryManagementFoldersViewMode");
        savedJson.Should().Contain("libraryFoldersViewMode");
        savedJson.Should().Contain("libraryManagementFolderSortBySection");
        savedJson.Should().Contain("libraryManagementMaterialSortByFolder");
        savedJson.Should().NotContain("libraryManagementTopicsViewMode");
        savedJson.Should().NotContain("libraryTopicsViewMode");
        savedJson.Should().NotContain("libraryManagementTopicSortBySection");
        savedJson.Should().NotContain("libraryManagementMaterialSortByTopic");
    }

    [Fact]
    public void NewFolderKeys_TakePrecedence_OverOldTopicKeys()
    {
        Guid sectionId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();

        string json = $$"""
            {
              "libraryManagementFoldersViewMode": "CompactTiles",
              "libraryFoldersViewMode": "Table",
              "libraryManagementFolderSortBySection": {
                "{{sectionId}}": "RecentActivity"
              },
              "libraryManagementMaterialSortByFolder": {
                "{{folderId}}": "Newest"
              },
              "libraryManagementTopicsViewMode": "Tiles",
              "libraryTopicsViewMode": "Tiles",
              "libraryManagementTopicSortBySection": {
                "{{sectionId}}": "Custom"
              },
              "libraryManagementMaterialSortByTopic": {
                "{{folderId}}": "Name"
              }
            }
            """;

        AppSettings settings = ReadSettings(json);

        settings.LibraryManagementFoldersViewMode.Should().Be(LibraryManagementViewMode.CompactTiles);
        settings.LibraryFoldersViewMode.Should().Be(LibraryFoldersViewMode.Table);
        settings.LibraryManagementFolderSortBySection[sectionId].Should().Be(LibraryManagementSortMode.RecentActivity);
        settings.LibraryManagementMaterialSortByFolder[folderId].Should().Be(LibraryManagementSortMode.Newest);
    }

    [Fact]
    public void LegacyGenericManagementViewMode_IsApplied_WhenFolderModesAreAbsent()
    {
        AppSettings settings = ReadSettings("""
            {
              "libraryManagementViewMode": "Table"
            }
            """);

        settings.LibraryManagementSectionsViewMode.Should().Be(LibraryManagementViewMode.Table);
        settings.LibraryManagementFoldersViewMode.Should().Be(LibraryManagementViewMode.Table);
        settings.LibraryManagementMaterialsViewMode.Should().Be(LibraryManagementViewMode.Table);
    }

    [Fact]
    public void OldFolderMode_TakesPrecedence_OverOldGenericManagementMode()
    {
        AppSettings settings = ReadSettings("""
            {
              "libraryManagementViewMode": "Table",
              "libraryManagementTopicsViewMode": "Tiles"
            }
            """);

        settings.LibraryManagementSectionsViewMode.Should().Be(LibraryManagementViewMode.Table);
        settings.LibraryManagementFoldersViewMode.Should().Be(LibraryManagementViewMode.Tiles);
        settings.LibraryManagementMaterialsViewMode.Should().Be(LibraryManagementViewMode.Table);
    }

    [Fact]
    public void OldAndNewSortDictionaries_AreMergedWithoutOverwritingNewKeys()
    {
        Guid sharedSectionId = Guid.NewGuid();
        Guid legacyOnlySectionId = Guid.NewGuid();

        string json = $$"""
            {
              "libraryManagementFolderSortBySection": {
                "{{sharedSectionId}}": "Newest"
              },
              "libraryManagementTopicSortBySection": {
                "{{sharedSectionId}}": "Name",
                "{{legacyOnlySectionId}}": "RecentActivity"
              }
            }
            """;

        AppSettings settings = ReadSettings(json);

        settings.LibraryManagementFolderSortBySection[sharedSectionId]
            .Should().Be(LibraryManagementSortMode.Newest);
        settings.LibraryManagementFolderSortBySection[legacyOnlySectionId]
            .Should().Be(LibraryManagementSortMode.RecentActivity);
    }

    [Fact]
    public void UnknownJsonSettings_ArePreserved_ButOldLibraryKeysAreRemoved()
    {
        AppSettings settings = ReadSettings("""
            {
              "libraryTopicsViewMode": "Tiles",
              "newerFeaturePreference": { "enabled": true }
            }
            """);

        string savedJson = JsonSerializer.Serialize(settings, JsonOptions);

        savedJson.Should().NotContain("libraryTopicsViewMode");
        savedJson.Should().Contain("newerFeaturePreference");
        using JsonDocument document = JsonDocument.Parse(savedJson);
        document.RootElement.GetProperty("newerFeaturePreference")
            .GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    private static AppSettings ReadSettings(string json)
    {
        AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)!;

        // JsonSettingsService performs this step after deserialization.
        MethodInfo method = typeof(AppSettings).GetMethod("ApplyLegacySettings", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(settings, null);

        return settings;
    }
}
