using System.Text.Json.Serialization;

namespace Mnemora.Desktop.Settings;

public sealed class AppSettings
{
    private LibraryOverviewViewMode _libraryOverviewViewMode = LibraryOverviewViewMode.Tiles;
    private LibraryManagementViewMode _libraryManagementSectionsViewMode = LibraryManagementViewMode.Tiles;
    private LibraryManagementViewMode _libraryManagementTopicsViewMode = LibraryManagementViewMode.Tiles;
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

    public LibraryManagementViewMode LibraryManagementTopicsViewMode
    {
        get => _libraryManagementTopicsViewMode;
        set
        {
            _libraryManagementTopicsViewMode = value;
            HasExplicitLibraryManagementTopicsViewMode = true;
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
            _libraryManagementTopicsViewMode = value;
            _libraryManagementMaterialsViewMode = value;
        }
    }

    public LibraryTopicsViewMode LibraryTopicsViewMode { get; set; } = LibraryTopicsViewMode.CompactTiles;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LibraryTilesPerRow { get; set; }

    public double LibraryContainerFoldersPaneRatio { get; set; } = 1d / 3d;

    public LibraryManagementSortMode LibraryManagementSectionSort { get; set; } = LibraryManagementSortMode.Custom;

    public Dictionary<Guid, LibraryManagementSortMode> LibraryManagementTopicSortBySection { get; set; } = [];

    public Dictionary<Guid, LibraryManagementSortMode> LibraryManagementMaterialSortByTopic { get; set; } = [];

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
    internal bool HasExplicitLibraryManagementTopicsViewMode { get; private set; }

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

            if (!HasExplicitLibraryManagementTopicsViewMode)
            {
                _libraryManagementTopicsViewMode = legacyManagementViewMode;
            }

            if (!HasExplicitLibraryManagementMaterialsViewMode)
            {
                _libraryManagementMaterialsViewMode = legacyManagementViewMode;
            }
        }

        LegacyLibraryViewMode = null;
        LegacyLibraryManagementViewMode = null;
    }
}
