using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetManagementMaterialsPage;
using Mnemora.Contracts;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryManagementMaterialsPageQueryHandlerTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(29, 29, false)]
    [InlineData(30, 30, false)]
    [InlineData(31, 30, true)]
    public async Task Boundaries_ReturnExactlyOneDatabasePage(
        int materialCount,
        int expectedItems,
        bool expectedHasMore)
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        Material[] materials = Enumerable.Range(0, materialCount)
            .Select(index => (Material)db.CreateArticle(topic.Id, $"Article {index:D5}"))
            .ToArray();
        await db.AddMaterialsAsync(materials);

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(topic.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(expectedItems);
        result.Value.TotalCount.Should().Be(materialCount);
        result.Value.SourceTotalCount.Should().Be(materialCount);
        result.Value.HasMore.Should().Be(expectedHasMore);
        result.Value.NextOffset.Should().Be(expectedItems);
    }


    [Fact]
    public async Task ContainerId_ScopesRootAndNestedFolderIndependently()
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
            FolderName.Create("Nested").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

        db.Context.LibraryContainers.Add(nestedFolder);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article rootArticle = db.CreateArticle(topic.Id, "Root article");
        rootArticle.MoveToContainer(root.Id).IsSuccess.Should().BeTrue();

        Article topicArticle = db.CreateArticle(topic.Id, "Topic folder article");

        Article nestedArticle = db.CreateArticle(topic.Id, "Nested article");
        nestedArticle.MoveToContainer(nestedFolder.Id).IsSuccess.Should().BeTrue();

        Question linkedQuestion = db.CreateLinkedQuestion(
            nestedArticle,
            "Nested linked question");

        await db.AddMaterialsAsync(
            rootArticle,
            topicArticle,
            nestedArticle,
            linkedQuestion);

        var sut = CreateHandler(db);

        var rootResult = await sut.Handle(Query(root.Id.Value));
        var nestedResult = await sut.Handle(Query(nestedFolder.Id.Value));

        rootResult.IsSuccess.Should().BeTrue();
        rootResult.Value.Items.Should().ContainSingle();
        rootResult.Value.Items[0].Id.Should().Be(rootArticle.Id.Value);
        rootResult.Value.Items[0].ContainerId.Should().Be(root.Id.Value);
        rootResult.Value.Items[0].TopicId.Should().Be(topic.Id.Value);

        nestedResult.IsSuccess.Should().BeTrue();
        nestedResult.Value.Items.Should().ContainSingle();
        nestedResult.Value.Items[0].Id.Should().Be(nestedArticle.Id.Value);
        nestedResult.Value.Items[0].ContainerId.Should().Be(nestedFolder.Id.Value);
        nestedResult.Value.Items[0].TopicId.Should().Be(topic.Id.Value);
        nestedResult.Value.Items[0].ArticleQuestionCount.Should().Be(1);
        nestedResult.Value.SourceTotalCount.Should().Be(1);
    }

    [Fact]
    public async Task LinkedQuestions_AreExcludedBeforePaginationAndFromTotals()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        var articles = Enumerable.Range(0, 30)
            .Select(index => db.CreateArticle(topic.Id, $"Article {index:D3}"))
            .ToArray();
        var standalone = Enumerable.Range(0, 10)
            .Select(index => db.CreateStandaloneQuestion(topic.Id, $"Standalone {index:D3}"))
            .ToArray();
        var linked = Enumerable.Range(0, 100)
            .Select(index => db.CreateLinkedQuestion(articles[index % articles.Length], $"Linked {index:D3}"))
            .ToArray();

        await db.AddMaterialsAsync(
            articles.Cast<Material>()
                .Concat(standalone)
                .Concat(linked)
                .ToArray());

        var sut = CreateHandler(db);
        var first = await sut.Handle(Query(topic.Id.Value, offset: 0));
        var second = await sut.Handle(Query(topic.Id.Value, offset: 30));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(30);
        second.Value.Items.Should().HaveCount(10);
        first.Value.TotalCount.Should().Be(40);
        first.Value.SourceTotalCount.Should().Be(40);
        first.Value.Items.Should().OnlyContain(item => item.Type == "Article" || item.Type == "Question");
        first.Value.Items.Concat(second.Value.Items)
            .Select(item => item.Id)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ArticleQuestionCount_IsCalculatedInOneGroupedQuery_NotNPlusOne()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        var articles = Enumerable.Range(0, 30)
            .Select(index => db.CreateArticle(topic.Id, $"Article {index:D3}"))
            .ToArray();
        var linked = articles
            .SelectMany((article, articleIndex) => Enumerable.Range(0, articleIndex % 7)
                .Select(questionIndex => db.CreateLinkedQuestion(
                    article,
                    $"Linked {articleIndex:D3}-{questionIndex:D2}")))
            .ToArray();

        await db.AddMaterialsAsync(articles.Cast<Material>().Concat(linked).ToArray());
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(topic.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);

        foreach (var item in result.Value.Items)
        {
            int articleIndex = int.Parse(item.Title["Article ".Length..]);
            item.ArticleQuestionCount.Should().Be(articleIndex % 7);
        }

        // Source count + page + grouped linked-question count. The unfiltered total reuses SourceTotalCount.
        // The number of SQL commands must stay constant when the page contains 30 articles.
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(4);
    }

    [Theory]
    [InlineData(LibraryManagementMaterialPageFilter.Articles, 12, "Article")]
    [InlineData(LibraryManagementMaterialPageFilter.Questions, 7, "Question")]
    public async Task TypeFilter_IsAppliedInSqlAndTotalCountMatchesFilteredSet(
        LibraryManagementMaterialPageFilter filter,
        int expectedCount,
        string expectedType)
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        var articles = Enumerable.Range(0, 12)
            .Select(index => db.CreateArticle(topic.Id, $"Article {index:D2}"));
        var questions = Enumerable.Range(0, 7)
            .Select(index => db.CreateStandaloneQuestion(topic.Id, $"Question {index:D2}"));
        await db.AddMaterialsAsync(articles.Cast<Material>().Concat(questions).ToArray());

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(topic.Id.Value, filter: filter));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(expectedCount);
        result.Value.Items.Should().OnlyContain(item => item.Type == expectedType);
        result.Value.TotalCount.Should().Be(expectedCount);
        result.Value.SourceTotalCount.Should().Be(19);
    }

    [Fact]
    public async Task UnicodeSearch_IsCaseInsensitiveAndDoesNotChangeSourceTotalCount()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        await db.AddMaterialsAsync(
            db.CreateArticle(topic.Id, "Отслеживание сущностей EF Core"),
            db.CreateArticle(topic.Id, "Индексы PostgreSQL"),
            db.CreateStandaloneQuestion(topic.Id, "Что такое ChangeTracker?"));

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(topic.Id.Value, search: "СУЩНОСТЕЙ"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Title.Should().Be("Отслеживание сущностей EF Core");
        result.Value.TotalCount.Should().Be(1);
        result.Value.SourceTotalCount.Should().Be(3);
    }

    [Theory]
    [InlineData(LibraryManagementMaterialPageSort.Custom)]
    [InlineData(LibraryManagementMaterialPageSort.RecentActivity)]
    [InlineData(LibraryManagementMaterialPageSort.Name)]
    [InlineData(LibraryManagementMaterialPageSort.Newest)]
    public async Task EverySort_HasStableTieBreakerAcrossPageBoundaries(
        LibraryManagementMaterialPageSort sort)
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        var materials = Enumerable.Range(0, 61)
            .Select(_ => db.CreateArticle(topic.Id, "Same title"))
            .ToArray();
        await db.AddMaterialsAsync(materials.Cast<Material>().ToArray());

        // Make every primary sort key equal. Only Id may decide ordering.
        await db.Context.Database.ExecuteSqlRawAsync(
            "UPDATE materials SET display_order = 0, created_at = '2026-01-01 00:00:00', updated_at = '2026-01-01 00:00:00'");
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var full = await sut.Handle(Query(
            topic.Id.Value,
            offset: 0,
            pageSize: 100,
            sort: sort));
        full.IsSuccess.Should().BeTrue();
        Guid[] expectedIds = full.Value.Items.Select(item => item.Id).ToArray();

        var actual = new List<Guid>();
        foreach (int offset in new[] { 0, 30, 60 })
        {
            var result = await sut.Handle(Query(
                topic.Id.Value,
                offset: offset,
                sort: sort));
            result.IsSuccess.Should().BeTrue();
            actual.AddRange(result.Value.Items.Select(item => item.Id));
        }

        actual.Should().Equal(expectedIds);
        actual.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task NegativeOffset_IsNormalizedAndOversizedPage_IsCappedAt100()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        await db.AddMaterialsAsync(Enumerable.Range(0, 150)
            .Select(index => (Material)db.CreateArticle(topic.Id, $"Article {index:D3}"))
            .ToArray());

        var sut = CreateHandler(db);
        var result = await sut.Handle(new GetLibraryManagementMaterialsPageQuery(
            topic.Id.Value,
            Search: null,
            LibraryManagementMaterialPageFilter.All,
            LibraryManagementMaterialPageSort.Name,
            Offset: -500,
            PageSize: 1_000));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(LibraryPagingDefaults.MaxQueryPageSize);
        result.Value.NextOffset.Should().Be(LibraryPagingDefaults.MaxQueryPageSize);
        result.Value.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task DeepOffset_5000Items_ReturnsOnlyRequestedWindow()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        await db.AddMaterialsInBatchesAsync(Enumerable.Range(0, 5_000)
            .Select(index => (Material)db.CreateArticle(topic.Id, $"Material {index:D6}")));

        db.CommandCounter.Reset();
        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(
            topic.Id.Value,
            offset: 4_980,
            sort: LibraryManagementMaterialPageSort.Name));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(20);
        result.Value.Items.First().Title.Should().Be("Material 004980");
        result.Value.Items.Last().Title.Should().Be("Material 004999");
        result.Value.TotalCount.Should().Be(5_000);
        result.Value.HasMore.Should().BeFalse();
        db.Context.ChangeTracker.Entries().Should().BeEmpty("read queries are AsNoTracking");
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task LargeLoad_50000TopLevelAnd25000Linked_RemainsPageBounded()
    {
        const int topLevelCount = 50_000;

        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, var topic) = await db.CreateSectionAndTopicAsync();

        IEnumerable<Material> Build()
        {
            for (int index = 0; index < topLevelCount; index++)
            {
                Article article = db.CreateArticle(topic.Id, $"Material {index:D7}");
                yield return article;

                if ((index & 1) == 0)
                {
                    yield return db.CreateLinkedQuestion(article, $"Linked {index:D7}");
                }
            }
        }

        await db.AddMaterialsInBatchesAsync(Build(), batchSize: 2_000);
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(
            topic.Id.Value,
            offset: 49_950,
            sort: LibraryManagementMaterialPageSort.Name));

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceTotalCount.Should().Be(topLevelCount);
        result.Value.TotalCount.Should().Be(topLevelCount);
        result.Value.Items.Should().HaveCount(LibraryPagingDefaults.PageSize);
        result.Value.Items.First().Title.Should().Be("Material 0049950");
        result.Value.Items.Last().Title.Should().Be("Material 0049979");
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(4);
    }

    private static GetLibraryManagementMaterialsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(db.Context, NullLogger<GetLibraryManagementMaterialsPageQueryHandler>.Instance);

    private static GetLibraryManagementMaterialsPageQuery Query(
        Guid containerId,
        string? search = null,
        LibraryManagementMaterialPageFilter filter = LibraryManagementMaterialPageFilter.All,
        LibraryManagementMaterialPageSort sort = LibraryManagementMaterialPageSort.Name,
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PageSize) =>
        new(containerId, search, filter, sort, offset, pageSize);
}
