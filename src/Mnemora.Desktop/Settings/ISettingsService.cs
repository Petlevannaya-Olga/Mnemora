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

    Task SaveLibraryManagementTopicsViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementMaterialsViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryTopicsViewModeAsync(
        LibraryTopicsViewMode viewMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementSectionSortAsync(
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementTopicSortAsync(
        Guid sectionId,
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default);

    Task SaveLibraryManagementMaterialSortAsync(
        Guid topicId,
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default);

    Task CompleteOnboardingAsync(
        bool isAiConfigured,
        CancellationToken cancellationToken = default);
}