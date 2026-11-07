using System.Text.Json;
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

    // Unknown keys are retained for forward compatibility. Old library keys
    // are consumed and removed by LegacyLibrarySettingsCompatibility on load.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalJsonSettings { get; set; }

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

    internal void ApplyLegacySettings() => LegacyLibrarySettingsCompatibility.Apply(this);
}
