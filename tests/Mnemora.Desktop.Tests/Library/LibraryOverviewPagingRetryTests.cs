using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetSectionsPage;
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

public sealed class LibraryOverviewPagingRetryTests
{
    private const int PageSize = LibraryPagingDefaults.PageSize;

    [Fact]
    public async Task RetryPageCommand_AfterPreviousPageFailure_ReloadsPreviousOffset()
    {
        var dispatcher = new StubQueryDispatcher();
        LibraryOverviewViewModel viewModel = CreateViewModel(dispatcher);
        await viewModel.LoadAsync();

        // В окне помещается 7 страниц, в кэше — 10. Сначала пролистываем
        // 13 страниц, затем возвращаем 3 страницы из кэша.
        for (int page = 1; page <= 12; page++)
        {
            await viewModel.LoadNextPageCommand.ExecuteAsync(null);
        }

        for (int page = 0; page < 3; page++)
        {
            await viewModel.LoadPreviousPageCommand.ExecuteAsync(null);
        }

        dispatcher.FailOnceAtOffset = 2 * PageSize;
        await viewModel.LoadPreviousPageCommand.ExecuteAsync(null);

        viewModel.HasPageError.Should().BeTrue();
        viewModel.PageErrorMessage.Should().Be("Сбой загрузки страницы");
        viewModel.LoadPreviousPageCommand.CanExecute(null).Should().BeFalse();
        viewModel.LoadNextPageCommand.CanExecute(null).Should().BeFalse();
        viewModel.RetryPageCommand.CanExecute(null).Should().BeTrue();

        await viewModel.RetryPageCommand.ExecuteAsync(null);

        dispatcher.RequestedOffsets.TakeLast(2)
            .Should().Equal(2 * PageSize, 2 * PageSize);
        viewModel.HasPageError.Should().BeFalse();
        viewModel.SectionWindowStartOffset.Should().Be(2 * PageSize);
    }

    [Fact]
    public async Task RetryPageCommand_AfterNextPageFailure_ReloadsNextOffset()
    {
        var dispatcher = new StubQueryDispatcher { FailOnceAtOffset = PageSize };
        LibraryOverviewViewModel viewModel = CreateViewModel(dispatcher);
        await viewModel.LoadAsync();
        await viewModel.LoadNextPageCommand.ExecuteAsync(null);

        viewModel.HasPageError.Should().BeTrue();
        viewModel.LoadNextPageCommand.CanExecute(null).Should().BeFalse();

        await viewModel.RetryPageCommand.ExecuteAsync(null);

        dispatcher.RequestedOffsets.Should().Equal(0, PageSize, PageSize);
        viewModel.HasPageError.Should().BeFalse();
        viewModel.SectionWindowEndOffset.Should().Be(2 * PageSize);
    }

    [Fact]
    public async Task Reload_ClearsFailedPageAndDisablesRetry()
    {
        var dispatcher = new StubQueryDispatcher { FailOnceAtOffset = PageSize };
        LibraryOverviewViewModel viewModel = CreateViewModel(dispatcher);
        await viewModel.LoadAsync();
        await viewModel.LoadNextPageCommand.ExecuteAsync(null);

        await viewModel.ReloadCommand.ExecuteAsync(null);

        viewModel.HasPageError.Should().BeFalse();
        viewModel.RetryPageCommand.CanExecute(null).Should().BeFalse();
        viewModel.SectionWindowStartOffset.Should().Be(0);
    }

    private static LibraryOverviewViewModel CreateViewModel(StubQueryDispatcher dispatcher) =>
        new(
            dispatcher,
            new StubPageNavigationService(),
            new StubSettingsService(),
            NullLogger<LibraryOverviewViewModel>.Instance);

    private sealed class StubQueryDispatcher : IQueryDispatcher
    {
        private const int TotalItems = 15 * LibraryPagingDefaults.PageSize;

        public int? FailOnceAtOffset { get; set; }

        public List<int> RequestedOffsets { get; } = [];

        public Task<Result<TResponse, Errors>> SendAsync<TQuery, TResponse>(
            TQuery query,
            CancellationToken cancellationToken = default)
            where TQuery : IQuery
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (query is not GetLibrarySectionsPageQuery page)
            {
                throw new NotSupportedException(query.GetType().Name);
            }

            RequestedOffsets.Add(page.Offset);
            if (FailOnceAtOffset == page.Offset)
            {
                FailOnceAtOffset = null;
                Errors errors = new Error("PAGE_TEST", "Сбой загрузки страницы", ErrorType.FAILURE);
                return Task.FromResult(Result.Failure<TResponse, Errors>(errors));
            }

            LibrarySectionOverviewDto[] items = Enumerable.Range(
                    page.Offset,
                    Math.Min(page.PageSize, TotalItems - page.Offset))
                .Select(CreateSection)
                .ToArray();

            object response = new LibrarySectionsPageDto(
                items,
                page.Offset + items.Length,
                page.Offset + items.Length < TotalItems,
                TotalItems);

            return Task.FromResult(
                Result.Success<TResponse, Errors>((TResponse)response));
        }

        private static LibrarySectionOverviewDto CreateSection(int index) =>
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                $"Раздел {index}",
                "Navy",
                "BookOpenVariant",
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                0, 0, 0, 0, 0);
    }

    private sealed class StubPageNavigationService : IPageNavigationService
    {
        public ViewModelBase? CurrentPageViewModel => null;
        public bool CanNavigateBack => false;
        private EventHandler? _currentPageViewModelChangedHandlers;

        public event EventHandler? CurrentPageViewModelChanged
        {
            add => _currentPageViewModelChangedHandlers += value;
            remove => _currentPageViewModelChangedHandlers -= value;
        }
        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase =>
            throw new NotSupportedException();
        public void NavigateTo<TViewModel>(Action<TViewModel> initialize)
            where TViewModel : ViewModelBase =>
            throw new NotSupportedException();
        public void NavigateBack() => throw new NotSupportedException();
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
        public Task SaveLibraryManagementViewModeAsync(LibraryManagementViewMode viewMode,
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
