using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mnemora.Desktop.Settings;

/// <summary>
/// Reads obsolete settings.json property names from earlier application versions.
/// New application settings use Folder/Container names exclusively.
/// </summary>
internal static class LegacyLibrarySettingsCompatibility
{
    private const string OldOverviewMode = "libraryViewMode";
    private const string OldManagementMode = "libraryManagementViewMode";
    private const string OldManagementFoldersMode = "libraryManagementTopicsViewMode";
    private const string OldFoldersMode = "libraryTopicsViewMode";
    private const string OldFolderSorts = "libraryManagementTopicSortBySection";
    private const string OldMaterialSorts = "libraryManagementMaterialSortByTopic";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    internal static void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Dictionary<string, JsonElement>? extensions = settings.AdditionalJsonSettings;
        if (extensions is null || extensions.Count == 0)
        {
            return;
        }

        LibraryOverviewViewMode? oldOverviewMode = ReadValue<LibraryOverviewViewMode>(
            extensions, OldOverviewMode);
        LibraryManagementViewMode? oldManagementMode = ReadValue<LibraryManagementViewMode>(
            extensions, OldManagementMode);
        LibraryManagementViewMode? oldManagementFoldersMode = ReadValue<LibraryManagementViewMode>(
            extensions, OldManagementFoldersMode);
        LibraryFoldersViewMode? oldFoldersMode = ReadValue<LibraryFoldersViewMode>(
            extensions, OldFoldersMode);

        if (!settings.HasExplicitLibraryOverviewViewMode && oldOverviewMode is { } overviewMode)
        {
            settings.LibraryOverviewViewMode = overviewMode;
        }

        if (!settings.HasExplicitLibraryManagementSectionsViewMode && oldManagementMode is { } sectionMode)
        {
            settings.LibraryManagementSectionsViewMode = sectionMode;
        }

        if (!settings.HasExplicitLibraryManagementFoldersViewMode)
        {
            // The previous folder-specific key takes priority over the older generic key.
            if ((oldManagementFoldersMode ?? oldManagementMode) is { } foldersMode)
            {
                settings.LibraryManagementFoldersViewMode = foldersMode;
            }
        }

        if (!settings.HasExplicitLibraryManagementMaterialsViewMode && oldManagementMode is { } materialsMode)
        {
            settings.LibraryManagementMaterialsViewMode = materialsMode;
        }

        if (!settings.HasExplicitLibraryFoldersViewMode && oldFoldersMode is { } foldersViewMode)
        {
            settings.LibraryFoldersViewMode = foldersViewMode;
        }

        MergeSorts(settings.LibraryManagementFolderSortBySection ??= [],
            ReadSorts(extensions, OldFolderSorts));
        MergeSorts(settings.LibraryManagementMaterialSortByFolder ??= [],
            ReadSorts(extensions, OldMaterialSorts));

        // Migrated fields must not be written back under obsolete JSON names.
        extensions.Remove(OldOverviewMode);
        extensions.Remove(OldManagementMode);
        extensions.Remove(OldManagementFoldersMode);
        extensions.Remove(OldFoldersMode);
        extensions.Remove(OldFolderSorts);
        extensions.Remove(OldMaterialSorts);

        if (extensions.Count == 0)
        {
            settings.AdditionalJsonSettings = null;
        }
    }

    private static T? ReadValue<T>(Dictionary<string, JsonElement> extensions, string key)
        where T : struct
    {
        if (!extensions.TryGetValue(key, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.Deserialize<T>(ReadOptions);
    }

    private static Dictionary<Guid, LibraryManagementSortMode>? ReadSorts(
        Dictionary<string, JsonElement> extensions, string key)
    {
        if (!extensions.TryGetValue(key, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.Deserialize<Dictionary<Guid, LibraryManagementSortMode>>(ReadOptions);
    }

    private static void MergeSorts(
        Dictionary<Guid, LibraryManagementSortMode> current,
        Dictionary<Guid, LibraryManagementSortMode>? old)
    {
        if (old is null)
        {
            return;
        }

        foreach ((Guid id, LibraryManagementSortMode sort) in old)
        {
            current.TryAdd(id, sort);
        }
    }
}
