using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetContainerContents;
using Mnemora.Application.Materials.GetDetails;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class ArticleViewModelTests
{
    [Fact]
    public async Task LoadAsync_LoadsArticleForInternalReading()
    {
        Guid articleId = Guid.NewGuid();
        ArticleDetailsDto details = CreateArticle(articleId, "# EF Core\n\nChange tracking.");
        var dispatcher = new StubQueryDispatcher(details);
        var navigation = new StubPageNavigationService();
        var editor = new StubMaterialEditorService();
        var viewModel = new ArticleViewModel(
            dispatcher,
            navigation,
            editor,
            NullLogger<ArticleViewModel>.Instance);

        viewModel.Initialize(articleId, LibraryArticleOpenOrigin.Overview);
        await viewModel.LoadAsync();

        viewModel.HasArticle.Should().BeTrue();
        viewModel.Title.Should().Be("EF Core");
        viewModel.SectionName.Should().Be("Backend");
        viewModel.FolderPath.Should().Be("C# / Entity Framework");
        viewModel.DifficultyText.Should().Be("Средний");
        viewModel.BodyMarkdown.Should().Be("# EF Core\n\nChange tracking.");
        viewModel.ErrorMessage.Should().BeNull();
        viewModel.Breadcrumbs.Select(item => item.Name)
            .Should().Equal("Backend", "C#", "Entity Framework");
        viewModel.Breadcrumbs[^1].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_UsesSectionAsCurrentBreadcrumbForSectionRoot()
    {
        Guid articleId = Guid.NewGuid();
        ArticleDetailsDto details = CreateArticle(articleId, "Text", "Без папки");
        var viewModel = new ArticleViewModel(
            new StubQueryDispatcher(details),
            new StubPageNavigationService(),
            new StubMaterialEditorService(),
            NullLogger<ArticleViewModel>.Instance);

        viewModel.Initialize(articleId, LibraryArticleOpenOrigin.Overview);
        await viewModel.LoadAsync();

        viewModel.Breadcrumbs.Select(item => item.Name)
            .Should().Equal("Backend");
        viewModel.Breadcrumbs[0].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task EditCommand_OpensCurrentArticleInConfiguredEditor()
    {
        Guid articleId = Guid.NewGuid();
        var dispatcher = new StubQueryDispatcher(CreateArticle(articleId, "Text"));
        var navigation = new StubPageNavigationService();
        var editor = new StubMaterialEditorService();
        var viewModel = new ArticleViewModel(
            dispatcher,
            navigation,
            editor,
            NullLogger<ArticleViewModel>.Instance);

        viewModel.Initialize(articleId, LibraryArticleOpenOrigin.Management);
        await viewModel.LoadAsync();
        await viewModel.EditCommand.ExecuteAsync(null);

        editor.OpenedArticleId.Should().Be(articleId);
    }

    private static ArticleDetailsDto CreateArticle(
        Guid articleId,
        string markdown,
        string containerPath = "C# / Entity Framework") =>
        new(
            new MaterialMetadataDto(
                articleId,
                Guid.NewGuid(),
                containerPath,
                Guid.NewGuid(),
                "Backend",
                "EF Core",
                "Article",
                "Medium",
                "FileDocumentOutline",
                20,
                10,
                1,
                ["ef-core", "orm"],
                DateTime.UtcNow,
                DateTime.UtcNow),
            markdown);

    private sealed class StubQueryDispatcher : IQueryDispatcher
    {
        private readonly MaterialDetailsDto _response;
        private readonly IReadOnlyDictionary<Guid, LibraryContainerContentsDto> _containers;

        public StubQueryDispatcher(MaterialDetailsDto response)
        {
            _response = response;
            _containers = BuildContainers(response.Metadata);
        }

        public Task<Result<TResponse, Errors>> SendAsync<TQuery, TResponse>(
            TQuery query,
            CancellationToken cancellationToken = default)
            where TQuery : IQuery
        {
            cancellationToken.ThrowIfCancellationRequested();

            object value = query switch
            {
                GetMaterialDetailsQuery => _response,
                GetLibraryContainerContentsQuery containerQuery => _containers[containerQuery.ContainerId],
                _ => throw new NotSupportedException(query.GetType().Name),
            };

            return Task.FromResult(
                Result.Success<TResponse, Errors>((TResponse)value));
        }

        private static IReadOnlyDictionary<Guid, LibraryContainerContentsDto> BuildContainers(
            MaterialMetadataDto metadata)
        {
            var result = new Dictionary<Guid, LibraryContainerContentsDto>();
            var section = new LibrarySectionHeaderDto(
                metadata.SectionId,
                metadata.SectionName,
                "Navy",
                "BookOpenVariant",
                DateTime.UtcNow,
                DateTime.UtcNow);

            if (metadata.ContainerPath == "Без папки")
            {
                AddContainer(result, section, metadata.ContainerId, null, 0, metadata.SectionName);
                return result;
            }

            string[] folderNames = metadata.ContainerPath
                .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Guid rootId = Guid.NewGuid();
            AddContainer(result, section, rootId, null, 0, metadata.SectionName);

            Guid parentId = rootId;
            for (int index = 0; index < folderNames.Length; index++)
            {
                Guid folderId = index == folderNames.Length - 1
                    ? metadata.ContainerId
                    : Guid.NewGuid();
                AddContainer(result, section, folderId, parentId, index + 1, folderNames[index]);
                parentId = folderId;
            }

            return result;
        }

        private static void AddContainer(
            IDictionary<Guid, LibraryContainerContentsDto> containers,
            LibrarySectionHeaderDto section,
            Guid id,
            Guid? parentId,
            int depth,
            string name)
        {
            var header = new LibraryContainerHeaderDto(
                id,
                section.Id,
                section.Name,
                parentId,
                depth,
                name,
                "Navy",
                "FolderOutline",
                DateTime.UtcNow,
                DateTime.UtcNow);
            containers[id] = new LibraryContainerContentsDto(header, section, 0, 0, depth < 3);
        }
    }

    private sealed class StubMaterialEditorService : IMaterialEditorService
    {
        public Guid? OpenedArticleId { get; private set; }

        public Task OpenArticleInEditorAsync(
            Guid articleId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenedArticleId = articleId;
            return Task.CompletedTask;
        }
    }

    private sealed class StubPageNavigationService : IPageNavigationService
    {
        public ViewModelBase? CurrentPageViewModel { get; private set; }

        public bool CanNavigateBack => false;

        public event EventHandler? CurrentPageViewModelChanged;

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase =>
            throw new NotSupportedException();

        public void NavigateTo<TViewModel>(Action<TViewModel> initialize)
            where TViewModel : ViewModelBase =>
            throw new NotSupportedException();

        public void NavigateBack() =>
            throw new NotSupportedException();
    }
}
