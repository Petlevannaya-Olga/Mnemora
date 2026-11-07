using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetContainerContents;
using Mnemora.Application.Library.GetFoldersPage;
using Mnemora.Application.Library.GetMaterialsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class LibraryContainerPagingRetryTests
{
    [Fact]
    public async Task Folders_RetryNextPage_PreservesLoadedItemsAndOffset()
    {
        var dispatcher = new StubQueryDispatcher(foldersCount: 80, materialsCount: 0)
        {
            FailFolderOnceAtOffset = LibraryPagingDefaults.PageSize,
        };
        LibraryContainerViewModel viewModel = CreateViewModel(dispatcher);
        await viewModel.LoadAsync();

        viewModel.Folders.Should().HaveCount(LibraryPagingDefaults.PageSize);
        await viewModel.LoadNextFoldersPageCommand.ExecuteAsync(null);

        viewModel.HasFoldersError.Should().BeTrue();
        viewModel.Folders.Should().HaveCount(LibraryPagingDefaults.PageSize);
        viewModel.FolderWindowStartOffset.Should().Be(0);
        viewModel.LoadNextFoldersPageCommand.CanExecute(null).Should().BeFalse();

        await viewModel.RetryFoldersCommand.ExecuteAsync(null);

        dispatcher.FolderOffsets.Should().Equal(0, LibraryPagingDefaults.PageSize, LibraryPagingDefaults.PageSize);
        viewModel.HasFoldersError.Should().BeFalse();
        viewModel.Folders.Should().HaveCount(2 * LibraryPagingDefaults.PageSize);
        viewModel.FolderWindowStartOffset.Should().Be(0);
    }

    [Fact]
    public async Task Folders_RetryPreviousPage_RestoresRequestedOffset()
    {
        var dispatcher = new StubQueryDispatcher(
            foldersCount: 15 * LibraryPagingDefaults.PageSize,
            materialsCount: 0);
        LibraryContainerViewModel viewModel = CreateViewModel(dispatcher);
        await viewModel.LoadAsync();

        for (int page = 1; page <= 12; page++)
        {
            await viewModel.LoadNextFoldersPageCommand.ExecuteAsync(null);
        }

        for (int page = 0; page < 3; page++)
        {
            await viewModel.LoadPreviousFoldersPageCommand.ExecuteAsync(null);
        }

        int failedOffset = 2 * LibraryPagingDefaults.PageSize;
        dispatcher.FailFolderOnceAtOffset = failedOffset;
        await viewModel.LoadPreviousFoldersPageCommand.ExecuteAsync(null);

        viewModel.HasFoldersError.Should().BeTrue();
        viewModel.LoadPreviousFoldersPageCommand.CanExecute(null).Should().BeFalse();

        await viewModel.RetryFoldersCommand.ExecuteAsync(null);

        dispatcher.FolderOffsets.TakeLast(2).Should().Equal(failedOffset, failedOffset);
        viewModel.HasFoldersError.Should().BeFalse();
        viewModel.FolderWindowStartOffset.Should().Be(failedOffset);
    }

    [Fact]
    public async Task Materials_RetryNextPage_PreservesLoadedItemsAndOffset()
    {
        var dispatcher = new StubQueryDispatcher(foldersCount: 0, materialsCount: 120)
        {
            FailMaterialOnceAtOffset = 50,
        };
        LibraryContainerViewModel viewModel = CreateViewModel(dispatcher);
        await viewModel.LoadAsync();

        viewModel.Materials.Should().HaveCount(50);
        await viewModel.LoadNextMaterialsPageCommand.ExecuteAsync(null);

        viewModel.HasMaterialsError.Should().BeTrue();
        viewModel.Materials.Should().HaveCount(50);
        viewModel.LoadNextMaterialsPageCommand.CanExecute(null).Should().BeFalse();

        await viewModel.RetryMaterialsCommand.ExecuteAsync(null);

        dispatcher.MaterialOffsets.Should().Equal(0, 50, 50);
        viewModel.HasMaterialsError.Should().BeFalse();
        viewModel.Materials.Should().HaveCount(100);
        viewModel.MaterialWindowStartOffset.Should().Be(0);
    }

    [Fact]
    public async Task Mixed_RetryNextPage_UsesSameMaterialOffsetWithoutReloadingMetadata()
    {
        var dispatcher = new StubQueryDispatcher(foldersCount: 1, materialsCount: 100)
        {
            FailMaterialOnceAtOffset = 49,
        };
        LibraryContainerViewModel viewModel = CreateViewModel(dispatcher);
        await viewModel.LoadAsync();

        viewModel.MixedContent.Should().HaveCount(50);
        int metadataCalls = dispatcher.MetadataCalls;
        await viewModel.LoadNextMixedPageCommand.ExecuteAsync(null);

        viewModel.HasMixedError.Should().BeTrue();
        viewModel.MixedContent.Should().HaveCount(50);
        viewModel.LoadNextMixedPageCommand.CanExecute(null).Should().BeFalse();

        await viewModel.RetryMixedCommand.ExecuteAsync(null);

        dispatcher.MaterialOffsets.TakeLast(2).Should().Equal(49, 49);
        dispatcher.MetadataCalls.Should().Be(metadataCalls);
        viewModel.HasMixedError.Should().BeFalse();
        viewModel.MixedContent.Should().HaveCount(100);
        viewModel.MixedWindowStartOffset.Should().Be(0);
    }

    private static LibraryContainerViewModel CreateViewModel(StubQueryDispatcher dispatcher)
    {
        var viewModel = new LibraryContainerViewModel(
            dispatcher,
            new StubPageNavigationService(),
            new StubSettingsService(),
            new StubMaterialNavigationService(),
            NullLogger<LibraryContainerViewModel>.Instance);
        viewModel.Initialize(dispatcher.ContainerId);
        return viewModel;
    }

    private sealed class StubQueryDispatcher(int foldersCount, int materialsCount) : IQueryDispatcher
    {
        public Guid ContainerId { get; } = Guid.NewGuid();
        private Guid SectionId { get; } = Guid.NewGuid();
        public int? FailFolderOnceAtOffset { get; set; }
        public int? FailMaterialOnceAtOffset { get; set; }
        public List<int> FolderOffsets { get; } = [];
        public List<int> MaterialOffsets { get; } = [];
        public int MetadataCalls { get; private set; }

        public Task<Result<TResponse, Errors>> SendAsync<TQuery, TResponse>(
            TQuery query,
            CancellationToken cancellationToken = default)
            where TQuery : IQuery
        {
            cancellationToken.ThrowIfCancellationRequested();
            object response;

            switch (query)
            {
                case GetLibraryContainerContentsQuery:
                    MetadataCalls++;
                    var header = CreateHeader();
                    response = new LibraryContainerContentsDto(
                        header,
                        new LibrarySectionHeaderDto(
                            SectionId, "Section", "Teal", "Folder", DateTime.UtcNow, DateTime.UtcNow),
                        foldersCount, materialsCount, true);
                    break;

                case GetLibraryFoldersPageQuery folders:
                    FolderOffsets.Add(folders.Offset);
                    if (FailFolderOnceAtOffset == folders.Offset)
                    {
                        FailFolderOnceAtOffset = null;
                        return Fail<TResponse>();
                    }
                    var folderItems = Enumerable.Range(
                            folders.Offset,
                            Math.Max(0, Math.Min(folders.PageSize, foldersCount - folders.Offset)))
                        .Select(index => new LibraryFolderDto(
                            Guid.NewGuid(), SectionId, ContainerId, 1,
                            $"Folder {index:D4}", "Teal", "Folder", index,
                            0, 0, true, DateTime.UtcNow, DateTime.UtcNow))
                        .ToArray();
                    response = new LibraryFoldersPageDto(
                        folderItems, folders.Offset + folderItems.Length,
                        folders.Offset + folderItems.Length < foldersCount, foldersCount);
                    break;

                case GetLibraryMaterialsPageQuery materials:
                    MaterialOffsets.Add(materials.Offset);
                    if (FailMaterialOnceAtOffset == materials.Offset)
                    {
                        FailMaterialOnceAtOffset = null;
                        return Fail<TResponse>();
                    }
                    var materialItems = Enumerable.Range(
                            materials.Offset,
                            Math.Max(0, Math.Min(materials.PageSize, materialsCount - materials.Offset)))
                        .Select(index => new LibraryMaterialDto(
                            Guid.NewGuid(), ContainerId, $"Article {index:D4}",
                            "Article", "Easy", "FileDocument", 1, 1, 1,
                            [], null, DateTime.UtcNow, DateTime.UtcNow))
                        .ToArray();
                    response = new LibraryMaterialsPageDto(
                        CreateHeader(), materialItems, materials.Offset + materialItems.Length,
                        materials.Offset + materialItems.Length < materialsCount, materialsCount);
                    break;

                default:
                    throw new NotSupportedException(query.GetType().Name);
            }

            return Task.FromResult(Result.Success<TResponse, Errors>((TResponse)response));
        }

        private LibraryContainerHeaderDto CreateHeader() =>
            new(ContainerId, SectionId, "Section", null, 0,
                "Section", "Teal", "Folder", DateTime.UtcNow, DateTime.UtcNow);

        private static Task<Result<TResponse, Errors>> Fail<TResponse>()
        {
            Errors error = new Error("TEST.PAGE", "Не удалось загрузить страницу", ErrorType.FAILURE);
            return Task.FromResult(Result.Failure<TResponse, Errors>(error));
        }
    }

    private sealed class StubPageNavigationService : IPageNavigationService
    {
        private EventHandler? _currentPageViewModelChangedHandlers;

        public ViewModelBase? CurrentPageViewModel => null;
        public bool CanNavigateBack => false;
        public event EventHandler? CurrentPageViewModelChanged
        {
            add => _currentPageViewModelChangedHandlers += value;
            remove => _currentPageViewModelChangedHandlers -= value;
        }

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase =>
            throw new NotSupportedException();
        public void NavigateTo<TViewModel>(Action<TViewModel> initialize)
            where TViewModel : ViewModelBase => throw new NotSupportedException();
        public void NavigateBack() => throw new NotSupportedException();
    }

    private sealed class StubMaterialNavigationService : IMaterialNavigationService
    {
        public void OpenArticle(Guid articleId, LibraryArticleOpenOrigin origin) =>
            throw new NotSupportedException();
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppSettings());
        public Task SaveUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task SaveStoragePathAsync(string storagePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task SaveMarkdownEditorAsync(MarkdownEditorType? editor, string? visualStudioCodePath,
            string? obsidianVaultPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryOverviewViewModeAsync(LibraryOverviewViewMode viewMode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryManagementSectionsViewModeAsync(LibraryManagementViewMode viewMode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryManagementFoldersViewModeAsync(LibraryManagementViewMode viewMode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryManagementMaterialsViewModeAsync(LibraryManagementViewMode viewMode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryFoldersViewModeAsync(LibraryFoldersViewMode viewMode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryTilesPerRowAsync(int? tilesPerRow,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryContainerFoldersPaneRatioAsync(double foldersPaneRatio,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryManagementSectionSortAsync(LibraryManagementSortMode sortMode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryManagementFolderSortAsync(Guid sectionId, LibraryManagementSortMode sortMode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryManagementMaterialSortAsync(Guid folderId, LibraryManagementSortMode sortMode,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryManagementSectionColumnsAsync(IReadOnlyCollection<LibraryManagementSectionColumn> columns,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveLibraryManagementContentColumnsAsync(IReadOnlyCollection<LibraryManagementContentColumn> columns,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CompleteOnboardingAsync(bool isAiConfigured,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
