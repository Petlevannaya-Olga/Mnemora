using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetMaterialsPage;
using Mnemora.Contracts;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryMaterialsPageTotalCountTests
{
    [Fact]
    public async Task Paging_51Materials_ReturnsTotalCountForEveryPage()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        Material[] materials = Enumerable.Range(0, 51)
            .Select(index => (Material)db.CreateArticle(topic.Id, $"Article {index:D2}"))
            .ToArray();

        await db.AddMaterialsAsync(materials);

        var sut = CreateHandler(db);

        var first = await sut.Handle(Query(topic.Id.Value, offset: 0));
        var second = await sut.Handle(Query(topic.Id.Value, offset: 50));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(50);
        second.Value.Items.Should().ContainSingle();
        first.Value.TotalCount.Should().Be(51);
        second.Value.TotalCount.Should().Be(51);
        first.Value.NextOffset.Should().Be(50);
        second.Value.NextOffset.Should().Be(51);
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
    }


    [Fact]
    public async Task ContainerQuery_ReadsDirectSectionAndNestedFolderMaterials()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, var topic) = await db.CreateSectionAndTopicAsync();

        LibraryContainer root = await db.Context.LibraryContainers
            .SingleAsync(container =>
                container.SectionId == section.Id &&
                container.ParentId == null);

        LibraryContainer topicFolder = await db.Context.LibraryContainers
            .SingleAsync(container =>
                container.Id == LibraryContainerId.Create(topic.Id.Value).Value);

        LibraryContainer nestedFolder = LibraryContainer.CreateFolder(
            topicFolder,
            FolderName.Create("Memory").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

        db.Context.LibraryContainers.Add(nestedFolder);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article rootArticle = db.CreateArticle(topic.Id, "Direct section article");
        rootArticle.MoveToContainer(root.Id).IsSuccess.Should().BeTrue();

        Article nestedArticle = db.CreateArticle(topic.Id, "Nested folder article");
        nestedArticle.MoveToContainer(nestedFolder.Id).IsSuccess.Should().BeTrue();

        Article topicArticle = db.CreateArticle(topic.Id, "First level folder article");

        await db.AddMaterialsAsync(
            rootArticle,
            nestedArticle,
            topicArticle);

        var sut = CreateHandler(db);

        var rootResult = await sut.Handle(Query(root.Id.Value, offset: 0));
        var nestedResult = await sut.Handle(Query(nestedFolder.Id.Value, offset: 0));

        rootResult.IsSuccess.Should().BeTrue();
        rootResult.Value.Items.Should().ContainSingle();
        rootResult.Value.Items[0].Id.Should().Be(rootArticle.Id.Value);
        rootResult.Value.Items[0].ContainerId.Should().Be(root.Id.Value);
        rootResult.Value.Container.Should().NotBeNull();
        rootResult.Value.Container!.IsRoot.Should().BeTrue();
        rootResult.Value.Container.Name.Should().Be(section.Name.Value);
        rootResult.Value.Topic.Name.Should().Be(section.Name.Value);

        nestedResult.IsSuccess.Should().BeTrue();
        nestedResult.Value.Items.Should().ContainSingle();
        nestedResult.Value.Items[0].Id.Should().Be(nestedArticle.Id.Value);
        nestedResult.Value.Items[0].ContainerId.Should().Be(nestedFolder.Id.Value);
        nestedResult.Value.Items[0].TopicId.Should().Be(topic.Id.Value);
        nestedResult.Value.Container.Should().NotBeNull();
        nestedResult.Value.Container!.Id.Should().Be(nestedFolder.Id.Value);
        nestedResult.Value.Container.ParentId.Should().Be(topicFolder.Id.Value);
        nestedResult.Value.Container.Depth.Should().Be(2);
        nestedResult.Value.Container.Name.Should().Be("Memory");
        nestedResult.Value.Topic.Name.Should().Be("Memory");
    }

    [Fact]
    public async Task FilterAndSearch_TotalCountMatchesDisplayedMaterials()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        await db.AddMaterialsAsync(
            db.CreateArticle(topic.Id, "EF Core tracking"),
            db.CreateArticle(topic.Id, "PostgreSQL indexes"),
            db.CreateStandaloneQuestion(topic.Id, "EF Core question"));

        var sut = CreateHandler(db);
        var result = await sut.Handle(new GetLibraryMaterialsPageQuery(
            topic.Id.Value,
            "ef CORE",
            LibraryMaterialFilter.Articles,
            LibraryMaterialSort.Name,
            0,
            50));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Title.Should().Be("EF Core tracking");
        result.Value.TotalCount.Should().Be(1);
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task LinkedQuestions_AreExcludedFromTopLevelPageAndTotals()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        Article article = db.CreateArticle(topic.Id, "Article");
        Question standalone = db.CreateStandaloneQuestion(topic.Id, "Standalone question");
        Question linked = db.CreateLinkedQuestion(article, "Linked question");
        await db.AddMaterialsAsync(article, standalone, linked);

        var sut = CreateHandler(db);
        var all = await sut.Handle(Query(topic.Id.Value, offset: 0));
        var questions = await sut.Handle(new GetLibraryMaterialsPageQuery(
            topic.Id.Value,
            Search: null,
            LibraryMaterialFilter.Questions,
            LibraryMaterialSort.Name,
            Offset: 0,
            PageSize: 50));

        all.IsSuccess.Should().BeTrue();
        all.Value.Items.Select(item => item.Id)
            .Should().BeEquivalentTo(new[] { article.Id.Value, standalone.Id.Value });
        all.Value.TotalCount.Should().Be(2);

        questions.IsSuccess.Should().BeTrue();
        questions.Value.Items.Should().ContainSingle();
        questions.Value.Items[0].Id.Should().Be(standalone.Id.Value);
        questions.Value.TotalCount.Should().Be(1);
    }

    private static GetLibraryMaterialsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(db.Context, NullLogger<GetLibraryMaterialsPageQueryHandler>.Instance);

    private static GetLibraryMaterialsPageQuery Query(
        Guid containerId,
        int offset,
        int pageSize = 50) =>
        new(
            containerId,
            Search: null,
            LibraryMaterialFilter.All,
            LibraryMaterialSort.Name,
            offset,
            pageSize);
}
