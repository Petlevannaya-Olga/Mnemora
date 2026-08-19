using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetManagementTopicsPage;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryManagementTopicsPageQueryHandlerTests
{
    [Fact]
    public async Task Paging_31Topics_Returns30Then1()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (var section, _) =
            await db.CreateSectionAndTopicAsync(
                topicName: "Topic 00");

        for (int index = 1; index < 31; index++)
        {
            Topic topic = Topic.Create(
                section.Id,
                TopicName.Create(
                    $"Topic {index:D2}").Value,
                Enum.GetValues<TopicColor>()[0],
                Enum.GetValues<TopicIcon>()[0]);

            db.Context.Topics.Add(topic);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);

        var first = await sut.Handle(
            Query(
                section.Id.Value,
                offset: 0));

        var second = await sut.Handle(
            Query(
                section.Id.Value,
                offset: 30));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(30);
        second.Value.Items.Should().ContainSingle();
        first.Value.TotalCount.Should().Be(31);
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task TopicCounters_ExcludeLinkedQuestionsFromTopLevelMaterials()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (var section, var topic) =
            await db.CreateSectionAndTopicAsync();

        Article article =
            db.CreateArticle(
                topic.Id,
                "Article");

        Question[] standalone = Enumerable
            .Range(0, 2)
            .Select(index =>
                db.CreateStandaloneQuestion(
                    topic.Id,
                    $"Standalone {index}"))
            .ToArray();

        Question[] linked = Enumerable
            .Range(0, 10)
            .Select(index =>
                db.CreateLinkedQuestion(
                    article,
                    $"Linked {index}"))
            .ToArray();

        await db.AddMaterialsAsync(
            new Material[] { article }
                .Concat(standalone)
                .Concat(linked)
                .ToArray());

        var sut = CreateHandler(db);

        var result = await sut.Handle(
            Query(section.Id.Value));

        result.IsSuccess.Should().BeTrue();

        var item = result.Value.Items
            .Single(x => x.Id == topic.Id.Value);

        item.ArticlesCount.Should().Be(1);
        item.QuestionsCount.Should().Be(2);
        item.MaterialsCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveAndRunsBeforePaging()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (var section, _) =
            await db.CreateSectionAndTopicAsync(
                topicName: "A Topic 00");

        // Fill more than one page with names that sort before the target.
        // If someone ever applies paging before search, the target disappears.
        for (int index = 1; index < 40; index++)
        {
            Topic topic = Topic.Create(
                section.Id,
                TopicName.Create(
                    $"A Topic {index:D2}").Value,
                Enum.GetValues<TopicColor>()[0],
                Enum.GetValues<TopicIcon>()[0]);

            db.Context.Topics.Add(topic);
        }

        Topic target = Topic.Create(
            section.Id,
            TopicName.Create(
                "ZZZ EF Core").Value,
            Enum.GetValues<TopicColor>()[0],
            Enum.GetValues<TopicIcon>()[0]);

        db.Context.Topics.Add(target);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);

        var result = await sut.Handle(
            Query(
                section.Id.Value,
                search: "zzz ef CORE",
                offset: 0,
                pageSize: 30));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be(target.Id.Value);
        result.Value.Items[0].Name.Should().Be("ZZZ EF Core");
        result.Value.TotalCount.Should().Be(1);
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task TopicWithoutMaterials_UsesTopicUpdatedAtAsLastActivity()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (var section, var topic) =
            await db.CreateSectionAndTopicAsync(
                topicName: "Empty topic");

        var sut = CreateHandler(db);

        var result = await sut.Handle(
            Query(section.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();

        var item = result.Value.Items.Single();

        item.Id.Should().Be(topic.Id.Value);
        item.MaterialsCount.Should().Be(0);
        item.ArticlesCount.Should().Be(0);
        item.QuestionsCount.Should().Be(0);
        item.LastActivityAt.Should().Be(topic.UpdatedAt);
    }


    [Fact]
    public async Task DeepOffset_5000Topics_ReturnsOnlyRequestedPage()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, _) = await db.CreateSectionAndTopicAsync(topicName: "Topic 00000");

        for (int index = 1; index < 5_000; index++)
        {
            db.Context.Topics.Add(Topic.Create(
                section.Id,
                TopicName.Create($"Topic {index:D5}").Value,
                Enum.GetValues<TopicColor>()[0],
                Enum.GetValues<TopicIcon>()[0]));
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(
            section.Id.Value,
            offset: 4_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.TotalCount.Should().Be(5_000);
        result.Value.NextOffset.Should().Be(4_980);
        result.Value.HasMore.Should().BeTrue();
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task LargeLoad_50000Topics_DeepPageRemainsBounded()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, _) = await db.CreateSectionAndTopicAsync(topicName: "Topic 00000");

        for (int index = 1; index < 50_000; index++)
        {
            db.Context.Topics.Add(Topic.Create(
                section.Id,
                TopicName.Create($"Topic {index:D5}").Value,
                Enum.GetValues<TopicColor>()[0],
                Enum.GetValues<TopicIcon>()[0]));
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(
            section.Id.Value,
            offset: 49_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.TotalCount.Should().Be(50_000);
        result.Value.NextOffset.Should().Be(49_980);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static GetLibraryManagementTopicsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(
            db.Context,
            NullLogger<GetLibraryManagementTopicsPageQueryHandler>.Instance);

    private static GetLibraryManagementTopicsPageQuery Query(
        Guid sectionId,
        string? search = null,
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PageSize) =>
        new(
            sectionId,
            search,
            LibraryManagementTopicPageSort.Name,
            offset,
            pageSize);
}
