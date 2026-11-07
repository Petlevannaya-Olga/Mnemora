using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.Materials.GetDetails;
using Mnemora.Application.IntegrationTests.Library;
using Mnemora.Contracts;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Materials;

public sealed class GetMaterialDetailsQueryHandlerTests
{
    [Fact]
    public async Task Handle_LoadsArticleFromFolder()
    {
        await using SqliteLibraryTestDatabase database =
            await SqliteLibraryTestDatabase.CreateAsync();

        Section section = CreateSection("Backend");
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer firstLevel = CreateFolder(root, "C#");
        LibraryContainer secondLevel = CreateFolder(firstLevel, "Entity Framework");
        Article article = CreateArticle(secondLevel.Id, "EF Core");

        database.Context.Sections.Add(section);
        database.Context.LibraryContainers.AddRange(root, firstLevel, secondLevel);
        database.Context.Materials.Add(article);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var handler = new GetMaterialDetailsQueryHandler(
            database.Context,
            new StubMaterialContentStore("# EF Core"),
            NullLogger<GetMaterialDetailsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetMaterialDetailsQuery(article.Id.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ArticleDetailsDto details = result.Value.Should().BeOfType<ArticleDetailsDto>().Subject;
        details.Metadata.ContainerId.Should().Be(secondLevel.Id.Value);
        details.Metadata.ContainerPath.Should().Be("C# / Entity Framework");
        details.Metadata.SectionId.Should().Be(section.Id.Value);
        details.Metadata.SectionName.Should().Be("Backend");
        details.BodyMarkdown.Should().Be("# EF Core");
    }

    [Fact]
    public async Task Handle_UsesWithoutFolderForArticleInSectionRoot()
    {
        await using SqliteLibraryTestDatabase database =
            await SqliteLibraryTestDatabase.CreateAsync();

        Section section = CreateSection("Backend");
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        Article article = CreateArticle(root.Id, "CLR");

        database.Context.Sections.Add(section);
        database.Context.LibraryContainers.Add(root);
        database.Context.Materials.Add(article);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var handler = new GetMaterialDetailsQueryHandler(
            database.Context,
            new StubMaterialContentStore("# CLR"),
            NullLogger<GetMaterialDetailsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetMaterialDetailsQuery(article.Id.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ArticleDetailsDto details = result.Value.Should().BeOfType<ArticleDetailsDto>().Subject;
        details.Metadata.ContainerPath.Should().Be("Без папки");
        details.Metadata.SectionName.Should().Be("Backend");
    }

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);

    private static LibraryContainer CreateFolder(
        LibraryContainer parent,
        string name) =>
        LibraryContainer.CreateFolder(
            parent,
            FolderName.Create(name).Value,
            Enum.GetValues<FolderColor>()[0],
            Enum.GetValues<FolderIcon>()[0]).Value;

    private static Article CreateArticle(
        LibraryContainerId containerId,
        string title) =>
        Article.CreateInContainer(
            containerId,
            MaterialTitle.Create(title).Value,
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value,
            Array.Empty<MaterialTag>()).Value;

    private sealed class StubMaterialContentStore(string markdown) : IMaterialContentStore
    {
        public Task<UnitResult<Error>> CreateArticleAsync(
            MaterialId materialId,
            ArticleContent content,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UnitResult<Error>> CreateQuestionAsync(
            MaterialId materialId,
            QuestionContent content,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<ArticleContent, Error>> ReadArticleAsync(
            MaterialId materialId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success<ArticleContent, Error>(
                ArticleContent.Create(markdown).Value));
        }

        public Task<Result<QuestionContent, Error>> ReadQuestionAsync(
            MaterialId materialId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public UnitResult<Error> Delete(
            MaterialId materialId,
            MaterialType materialType) =>
            throw new NotSupportedException();
    }
}
