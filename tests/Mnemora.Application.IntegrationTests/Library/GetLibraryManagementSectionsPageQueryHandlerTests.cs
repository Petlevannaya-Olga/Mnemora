using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetManagementSectionsPage;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryManagementSectionsPageQueryHandlerTests
{
    [Fact]
    public async Task Paging_31Sections_Returns30Then1()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 31; index++)
        {
            Section section = Section.Create(
                SectionName.Create($"Section {index:D2}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]);
            db.Context.Sections.Add(section);
        }
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var first = await sut.Handle(Query(offset: 0));
        var second = await sut.Handle(Query(offset: 30));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(30);
        second.Value.Items.Should().ContainSingle();
        first.Value.TotalCount.Should().Be(31);
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task SectionCounters_ExcludeLinkedQuestionsFromTopLevelMaterials()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (var section, var topic) = await db.CreateSectionAndTopicAsync();

        Article article = db.CreateArticle(topic.Id, "Article");
        Question standalone = db.CreateStandaloneQuestion(topic.Id, "Standalone");
        Question[] linked = Enumerable.Range(0, 20)
            .Select(index => db.CreateLinkedQuestion(article, $"Linked {index}"))
            .ToArray();

        await db.AddMaterialsAsync(new Material[] { article, standalone }
            .Concat(linked)
            .ToArray());

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query());

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Single(x => x.Id == section.Id.Value);
        item.TopicsCount.Should().Be(1);
        item.ArticlesCount.Should().Be(1);
        item.QuestionsCount.Should().Be(1);
        item.MaterialsCount.Should().Be(2);
    }

    [Fact]
    public async Task NegativeOffsetAndOversizedPage_AreNormalized()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 120; index++)
        {
            db.Context.Sections.Add(Section.Create(
                SectionName.Create($"Section {index:D3}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]));
        }
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var result = await sut.Handle(new GetLibraryManagementSectionsPageQuery(
            Search: null,
            LibraryManagementSectionSort.Name,
            Offset: -10,
            PageSize: 1_000));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(LibraryPagingDefaults.MaxQueryPageSize);
        result.Value.NextOffset.Should().Be(LibraryPagingDefaults.MaxQueryPageSize);
        result.Value.HasMore.Should().BeTrue();
    }


    [Fact]
    public async Task Search_IsCaseInsensitiveAndRunsBeforePaging()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 40; index++)
        {
            db.Context.Sections.Add(Section.Create(
                SectionName.Create($"A Section {index:D2}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]));
        }

        Section target = Section.Create(
            SectionName.Create("ZZZ Backend").Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);
        db.Context.Sections.Add(target);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var result = await sut.Handle(new GetLibraryManagementSectionsPageQuery(
            Search: "zzz BACKEND",
            LibraryManagementSectionSort.Name,
            Offset: 0,
            PageSize: 30));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be(target.Id.Value);
        result.Value.TotalCount.Should().Be(1);
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task DeepOffset_5000Sections_ReturnsOnlyRequestedPage()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 5_000; index++)
        {
            db.Context.Sections.Add(Section.Create(
                SectionName.Create($"Section {index:D5}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]));
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(offset: 4_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.TotalCount.Should().Be(5_000);
        result.Value.NextOffset.Should().Be(4_980);
        result.Value.HasMore.Should().BeTrue();
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task LargeLoad_50000Sections_DeepPageRemainsBounded()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 50_000; index++)
        {
            db.Context.Sections.Add(Section.Create(
                SectionName.Create($"Section {index:D5}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]));
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(offset: 49_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.TotalCount.Should().Be(50_000);
        result.Value.NextOffset.Should().Be(49_980);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }


    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task SectionCounters_50000Topics_AreAggregatedInDatabase()
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
        var result = await sut.Handle(Query());

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].TopicsCount.Should().Be(50_000);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static GetLibraryManagementSectionsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(db.Context, NullLogger<GetLibraryManagementSectionsPageQueryHandler>.Instance);

    private static GetLibraryManagementSectionsPageQuery Query(
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PageSize) =>
        new(Search: null, LibraryManagementSectionSort.Name, offset, pageSize);
}
