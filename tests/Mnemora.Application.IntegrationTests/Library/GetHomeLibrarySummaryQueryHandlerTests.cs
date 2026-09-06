using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.Home.GetLibrarySummary;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetHomeLibrarySummaryQueryHandlerTests
{
    [Fact]
    public async Task EmptyLibrary_ReturnsZeroCountsAndNoSuggestedSection()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        var sut = CreateHandler(db.Context);
        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(0);
        result.Value.TopicsCount.Should().Be(0);
        result.Value.SuggestedSection.Should().BeNull();
        db.CommandCounter.Count.Should().Be(1);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task SectionWithoutTopics_IsPreferredForSuggestedSection()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        await db.CreateSectionAndTopicAsync(
            sectionName: "Populated section",
            topicName: "Topic");

        Section emptySection = CreateSection("Empty section");
        db.Context.Sections.Add(emptySection);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db.Context);
        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(2);
        result.Value.TopicsCount.Should().Be(1);
        var suggestedSection = result.Value.SuggestedSection;
        suggestedSection.Should().NotBeNull();
        suggestedSection!.Id.Should().Be(emptySection.Id.Value);
        suggestedSection!.Name.Should().Be(emptySection.Name.Value);
        db.CommandCounter.Count.Should().Be(3);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task AllSectionsHaveTopics_FallsBackToFirstSection()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, _) = await db.CreateSectionAndTopicAsync();

        var sut = CreateHandler(db.Context);
        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(1);
        result.Value.TopicsCount.Should().Be(1);
        result.Value.SuggestedSection.Should().NotBeNull();
        result.Value.SuggestedSection!.Id.Should().Be(section.Id.Value);
        db.CommandCounter.Count.Should().Be(4);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_DoesNotAccessMaterialsQuery()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        await db.CreateSectionAndTopicAsync();

        var guardedContext = new MaterialsForbiddenReadDbContext(db.Context);
        var sut = CreateHandler(guardedContext);

        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(1);
        result.Value.TopicsCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task LargeLoad_50000SectionsAndTopics_RemainsAggregateBounded()
    {
        const int targetCount = 50_000;
        const int batchSize = 2_000;

        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        SectionId? hotSectionId = null;

        for (int start = 0; start < targetCount; start += batchSize)
        {
            int end = Math.Min(start + batchSize, targetCount);
            var batch = new List<Section>(end - start);

            for (int index = start; index < end; index++)
            {
                Section section = CreateSection($"Section {index:D5}");
                hotSectionId ??= section.Id;
                batch.Add(section);
            }

            db.Context.Sections.AddRange(batch);
            await db.Context.SaveChangesAsync();
            db.Context.ChangeTracker.Clear();
        }

        hotSectionId.Should().NotBeNull();

        for (int start = 0; start < targetCount; start += batchSize)
        {
            int end = Math.Min(start + batchSize, targetCount);
            var batch = new List<Topic>(end - start);

            for (int index = start; index < end; index++)
            {
                batch.Add(Topic.Create(
                    hotSectionId!,
                    TopicName.Create($"Topic {index:D5}").Value,
                    Enum.GetValues<TopicColor>()[0],
                    Enum.GetValues<TopicIcon>()[0]));
            }

            db.Context.Topics.AddRange(batch);
            await db.Context.SaveChangesAsync();
            db.Context.ChangeTracker.Clear();
        }

        db.CommandCounter.Reset();

        var guardedContext = new MaterialsForbiddenReadDbContext(db.Context);
        var sut = CreateHandler(guardedContext);
        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(targetCount);
        result.Value.TopicsCount.Should().Be(targetCount);
        result.Value.SuggestedSection.Should().NotBeNull();
        result.Value.SuggestedSection!.Id.Should().NotBe(hotSectionId!.Value);
        db.CommandCounter.Count.Should().Be(3);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static GetHomeLibrarySummaryQueryHandler CreateHandler(
        IReadDbContext readDbContext) =>
        new(
            readDbContext,
            NullLogger<GetHomeLibrarySummaryQueryHandler>.Instance);

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);

    private sealed class MaterialsForbiddenReadDbContext(
        IReadDbContext inner) : IReadDbContext
    {
        public IQueryable<Section> SectionsRead => inner.SectionsRead;

        public IQueryable<LibraryContainer> LibraryContainersRead =>
            inner.LibraryContainersRead;

        public IQueryable<Topic> TopicsRead => inner.TopicsRead;

        public IQueryable<Material> MaterialsRead =>
            throw new InvalidOperationException(
                "Главная страница не должна обращаться к материалам.");
    }
}
