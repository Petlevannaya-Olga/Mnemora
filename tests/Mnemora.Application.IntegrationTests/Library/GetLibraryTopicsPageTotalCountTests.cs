using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetTopicsPage;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryTopicsPageTotalCountTests
{
    [Fact]
    public async Task Paging_31Topics_ReturnsTotalCountForEveryPage()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, _) = await db.CreateSectionAndTopicAsync(topicName: "Topic 00");

        for (int index = 1; index < 31; index++)
        {
            db.Context.Topics.Add(Topic.Create(
                section.Id,
                TopicName.Create($"Topic {index:D2}").Value,
                Enum.GetValues<TopicColor>()[0],
                Enum.GetValues<TopicIcon>()[0]));
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);

        var first = await sut.Handle(Query(section.Id.Value, offset: 0));
        var second = await sut.Handle(Query(section.Id.Value, offset: 30));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(30);
        second.Value.Items.Should().ContainSingle();
        first.Value.TotalCount.Should().Be(31);
        second.Value.TotalCount.Should().Be(31);
        first.Value.NextOffset.Should().Be(30);
        second.Value.NextOffset.Should().Be(31);
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Search_TotalCountMatchesFilteredTopicsBeforePaging()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, _) = await db.CreateSectionAndTopicAsync(topicName: "EF Core");

        db.Context.Topics.Add(Topic.Create(
            section.Id,
            TopicName.Create("PostgreSQL").Value,
            Enum.GetValues<TopicColor>()[0],
            Enum.GetValues<TopicIcon>()[0]));

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(section.Id.Value, search: "ef CORE"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Name.Should().Be("EF Core");
        result.Value.TotalCount.Should().Be(1);
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task TopicCounters_ExcludeQuestionsLinkedToArticles()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, var topic) = await db.CreateSectionAndTopicAsync();

        Article article = db.CreateArticle(topic.Id, "Article");
        Question standalone = db.CreateStandaloneQuestion(topic.Id, "Standalone question");
        Question linked = db.CreateLinkedQuestion(article, "Linked question");
        await db.AddMaterialsAsync(article, standalone, linked);

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(section.Id.Value));

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Single();
        item.ArticlesCount.Should().Be(1);
        item.QuestionsCount.Should().Be(1);
        item.MaterialsCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task TopicCounters_50000Materials_AreAggregatedInDatabase()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, var topic) = await db.CreateSectionAndTopicAsync();
        await db.AddMaterialsInBatchesAsync(
            Enumerable.Range(0, 50_000)
                .Select(index => (Material)db.CreateArticle(
                    topic.Id,
                    $"Article {index:D5}")));
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(section.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ArticlesCount.Should().Be(50_000);
        result.Value.Items[0].MaterialsCount.Should().Be(50_000);
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(6);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static GetLibraryTopicsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(db.Context, NullLogger<GetLibraryTopicsPageQueryHandler>.Instance);

    private static GetLibraryTopicsPageQuery Query(
        Guid sectionId,
        string? search = null,
        int offset = 0,
        int pageSize = 30) =>
        new(
            sectionId,
            search,
            LibraryTopicSort.Name,
            offset,
            pageSize);
}
