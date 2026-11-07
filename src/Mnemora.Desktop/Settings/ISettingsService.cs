namespace Mnemora.Desktop.Settings;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task SaveStoragePathAsync(string storagePath, CancellationToken cancellationToken = default);

    Task SaveMarkdownEditorAsync(
        MarkdownEditorType? editor,
        string? visualStudioCodePath,
        string? obsidianVaultPath,
        CancellationToken cancellationToken = default);

    Task SaveLibraryOverviewViewModeAsync(
        LibraryOverviewViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementSectionsViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementFoldersViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementMaterialsViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryFoldersViewModeAsync(
        LibraryFoldersViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryTilesPerRowAsync(
        int? tilesPerRow,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task SaveLibraryContainerFoldersPaneRatioAsync(
        double foldersPaneRatio,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    Task SaveLibraryManagementSectionSortAsync(
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementFolderSortAsync(
        Guid sectionId,
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementMaterialSortAsync(
        Guid folderId,
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementSectionColumnsAsync(
        IReadOnlyCollection<LibraryManagementSectionColumn> columns,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementContentColumnsAsync(
        IReadOnlyCollection<LibraryManagementContentColumn> columns,
        CancellationToken cancellationToken = default);

    Task CompleteOnboardingAsync(
        bool isAiConfigured,
        CancellationToken cancellationToken = default);
}