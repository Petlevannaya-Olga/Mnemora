using System.Text.Json.Serialization;

namespace Mnemora.Desktop.Settings;

public sealed class AppSettings
{
    private LibraryOverviewViewMode _libraryOverviewViewMode = LibraryOverviewViewMode.Tiles;
    private LibraryManagementViewMode _libraryManagementSectionsViewMode = LibraryManagementViewMode.Tiles;
    private LibraryManagementViewMode _libraryManagementFoldersViewMode = LibraryManagementViewMode.Tiles;
    private LibraryManagementViewMode _libraryManagementMaterialsViewMode = LibraryManagementViewMode.Table;

    public string? UserName { get; set; }

    public string? StoragePath { get; set; }

    public MarkdownEditorType? MarkdownEditor { get; set; }

    public string? VisualStudioCodePath { get; set; }

    public string? ObsidianVaultPath { get; set; }

    public bool IsAiConfigured { get; set; }

    public bool IsOnboardingCompleted { get; set; }

    public LibraryOverviewViewMode LibraryOverviewViewMode
    {
        get => _libraryOverviewViewMode;
        set
        {
            _libraryOverviewViewMode = value;
            HasExplicitLibraryOverviewViewMode = true;
        }
    }

    public LibraryManagementViewMode LibraryManagementSectionsViewMode
    {
        get => _libraryManagementSectionsViewMode;
        set
        {
            _libraryManagementSectionsViewMode = value;
            HasExplicitLibraryManagementSectionsViewMode = true;
        }
    }

    public LibraryManagementViewMode LibraryManagementFoldersViewMode
    {
        get => _libraryManagementFoldersViewMode;
        set
        {
            _libraryManagementFoldersViewMode = value;
            HasExplicitLibraryManagementFoldersViewMode = true;
        }
    }

    public LibraryManagementViewMode LibraryManagementMaterialsViewMode
    {
        get => _libraryManagementMaterialsViewMode;
        set
        {
            _libraryManagementMaterialsViewMode = value;
            HasExplicitLibraryManagementMaterialsViewMode = true;
        }
    }

    // Совместимость со старым Desktop-кодом. Новое управление библиотекой
    // использует три независимых свойства выше.
    [JsonIgnore]
    public LibraryManagementViewMode LibraryManagementViewMode
    {
        get => _libraryManagementSectionsViewMode;
        set
        {
            _libraryManagementSectionsViewMode = value;
            _libraryManagementFoldersViewMode = value;
            _libraryManagementMaterialsViewMode = value;
        }
    }

    private LibraryFoldersViewMode _libraryFoldersViewMode = LibraryFoldersViewMode.CompactTiles;

    public LibraryFoldersViewMode LibraryFoldersViewMode
    {
        get => _libraryFoldersViewMode;
        set
        {
            _libraryFoldersViewMode = value;
            HasExplicitLibraryFoldersViewMode = true;
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LibraryTilesPerRow { get; set; }

    public double LibraryContainerFoldersPaneRatio { get; set; } = 1d / 3d;

    public LibraryManagementSortMode LibraryManagementSectionSort { get; set; } = LibraryManagementSortMode.Custom;

    public Dictionary<Guid, LibraryManagementSortMode> LibraryManagementFolderSortBySection { get; set; } = [];

    public Dictionary<Guid, LibraryManagementSortMode> LibraryManagementMaterialSortByFolder { get; set; } = [];

    public HashSet<LibraryManagementSectionColumn> LibraryManagementSectionColumns { get; set; } =
    [
        LibraryManagementSectionColumn.Folders,
        LibraryManagementSectionColumn.Materials,
        LibraryManagementSectionColumn.Articles,
        LibraryManagementSectionColumn.Questions,
        LibraryManagementSectionColumn.Created,
        LibraryManagementSectionColumn.Updated,
        LibraryManagementSectionColumn.Activity,
    ];

    public HashSet<LibraryManagementContentColumn> LibraryManagementContentColumns { get; set; } =
    [
        LibraryManagementContentColumn.Type,
        LibraryManagementContentColumn.Location,
        LibraryManagementContentColumn.Questions,
        LibraryManagementContentColumn.Difficulty,
        LibraryManagementContentColumn.Updated,
    ];

    // Read-only compatibility aliases for settings.json written before the folder migration.
    // New settings are serialized under the Folder property names.
    [JsonPropertyName("libraryManagementTopicsViewMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LibraryManagementViewMode? LegacyLibraryManagementTopicsViewMode { get; set; }

    [JsonPropertyName("libraryTopicsViewMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LibraryFoldersViewMode? LegacyLibraryTopicsViewMode { get; set; }

    [JsonPropertyName("libraryManagementTopicSortBySection")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<Guid, LibraryManagementSortMode>? LegacyLibraryManagementTopicSortBySection { get; set; }

    [JsonPropertyName("libraryManagementMaterialSortByTopic")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<Guid, LibraryManagementSortMode>? LegacyLibraryManagementMaterialSortByTopic { get; set; }

    [JsonPropertyName("libraryViewMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LibraryOverviewViewMode? LegacyLibraryViewMode { get; set; }

    [JsonPropertyName("libraryManagementViewMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LibraryManagementViewMode? LegacyLibraryManagementViewMode { get; set; }

    [JsonIgnore]
    internal bool HasExplicitLibraryOverviewViewMode { get; private set; }

    [JsonIgnore]
    internal bool HasExplicitLibraryManagementSectionsViewMode { get; private set; }

    [JsonIgnore]
    internal bool HasExplicitLibraryFoldersViewMode { get; private set; }

    [JsonIgnore]
    internal bool HasExplicitLibraryManagementFoldersViewMode { get; private set; }

    [JsonIgnore]
    internal bool HasExplicitLibraryManagementMaterialsViewMode { get; private set; }

    internal void ApplyLegacySettings()
    {
        if (!HasExplicitLibraryOverviewViewMode && LegacyLibraryViewMode is { } legacyViewMode)
        {
            _libraryOverviewViewMode = legacyViewMode;
        }

        if (LegacyLibraryManagementViewMode is { } legacyManagementViewMode)
        {
            if (!HasExplicitLibraryManagementSectionsViewMode)
            {
                _libraryManagementSectionsViewMode = legacyManagementViewMode;
            }

            if (!HasExplicitLibraryManagementFoldersViewMode)
            {
                _libraryManagementFoldersViewMode = legacyManagementViewMode;
            }

            if (!HasExplicitLibraryManagementMaterialsViewMode)
            {
                _libraryManagementMaterialsViewMode = legacyManagementViewMode;
            }
        }

        // Prefer a new property if both old and new JSON keys are present.
        if (!HasExplicitLibraryManagementFoldersViewMode &&
            LegacyLibraryManagementTopicsViewMode is { } legacyFoldersViewMode)
        {
            _libraryManagementFoldersViewMode = legacyFoldersViewMode;
        }

        if (!HasExplicitLibraryFoldersViewMode &&
            LegacyLibraryTopicsViewMode is { } legacyViewMode)
        {
            _libraryFoldersViewMode = legacyViewMode;
        }

        // Merge dictionary entries without overriding values already saved with new names.
        LibraryManagementFolderSortBySection ??= [];
        if (LegacyLibraryManagementTopicSortBySection is not null)
        {
            foreach (var (sectionId, sortMode) in LegacyLibraryManagementTopicSortBySection)
            {
                LibraryManagementFolderSortBySection.TryAdd(sectionId, sortMode);
            }
        }

        LibraryManagementMaterialSortByFolder ??= [];
        if (LegacyLibraryManagementMaterialSortByTopic is not null)
        {
            foreach (var (folderId, sortMode) in LegacyLibraryManagementMaterialSortByTopic)
            {
                LibraryManagementMaterialSortByFolder.TryAdd(folderId, sortMode);
            }
        }

        LegacyLibraryManagementTopicsViewMode = null;
        LegacyLibraryTopicsViewMode = null;
        LegacyLibraryManagementTopicSortBySection = null;
        LegacyLibraryManagementMaterialSortByTopic = null;

        LegacyLibraryViewMode = null;
        LegacyLibraryManagementViewMode = null;
    }
}
