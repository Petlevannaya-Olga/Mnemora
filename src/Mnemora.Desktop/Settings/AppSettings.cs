using System.Text.Json.Serialization;

namespace Mnemora.Desktop.Settings;

public sealed class AppSettings
{
    public string? UserName { get; set; }
    public string? StoragePath { get; set; }
    public MarkdownEditorType? MarkdownEditor { get; set; }
    public string? VisualStudioCodePath { get; set; }
    public string? ObsidianVaultPath { get; set; }
    public bool IsAiConfigured { get; set; }
    public bool IsOnboardingCompleted { get; set; }

    public LibraryOverviewViewMode LibraryOverviewViewMode { get; set; } = LibraryOverviewViewMode.TILES;
    public LibraryManagementViewMode LibraryManagementSectionsViewMode { get; set; } = LibraryManagementViewMode.TILES;
    public LibraryManagementViewMode LibraryManagementFoldersViewMode { get; set; } = LibraryManagementViewMode.TILES;
    public LibraryManagementViewMode LibraryManagementMaterialsViewMode { get; set; } = LibraryManagementViewMode.TABLE;
    public LibraryFoldersViewMode LibraryFoldersViewMode { get; set; } = LibraryFoldersViewMode.COMPACT_TILES;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LibraryTilesPerRow { get; set; }

    public double LibraryContainerFoldersPaneRatio { get; set; } = 1d / 3d;
    public LibraryManagementSortMode LibraryManagementSectionSort { get; set; } = LibraryManagementSortMode.CUSTOM;
    public Dictionary<Guid, LibraryManagementSortMode> LibraryManagementFolderSortBySection { get; set; } = [];
    public Dictionary<Guid, LibraryManagementSortMode> LibraryManagementMaterialSortByFolder { get; set; } = [];

    public HashSet<LibraryManagementSectionColumn> LibraryManagementSectionColumns { get; set; } =
    [
        LibraryManagementSectionColumn.FOLDERS,
        LibraryManagementSectionColumn.MATERIALS,
        LibraryManagementSectionColumn.ARTICLES,
        LibraryManagementSectionColumn.QUESTIONS,
        LibraryManagementSectionColumn.CREATED,
        LibraryManagementSectionColumn.UPDATED,
        LibraryManagementSectionColumn.ACTIVITY,
    ];

    public HashSet<LibraryManagementContentColumn> LibraryManagementContentColumns { get; set; } =
    [
        LibraryManagementContentColumn.TYPE,
        LibraryManagementContentColumn.LOCATION,
        LibraryManagementContentColumn.QUESTIONS,
        LibraryManagementContentColumn.DIFFICULTY,
        LibraryManagementContentColumn.UPDATED,
    ];
}
