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

    public LibraryOverviewViewMode LibraryOverviewViewMode { get; set; } = LibraryOverviewViewMode.Tiles;
    public LibraryManagementViewMode LibraryManagementSectionsViewMode { get; set; } = LibraryManagementViewMode.Tiles;
    public LibraryManagementViewMode LibraryManagementFoldersViewMode { get; set; } = LibraryManagementViewMode.Tiles;
    public LibraryManagementViewMode LibraryManagementMaterialsViewMode { get; set; } = LibraryManagementViewMode.Table;
    public LibraryFoldersViewMode LibraryFoldersViewMode { get; set; } = LibraryFoldersViewMode.CompactTiles;

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
}
