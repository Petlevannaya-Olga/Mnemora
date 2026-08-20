using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetMaterialsPage;
using Mnemora.Contracts;
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

    private static GetLibraryMaterialsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(db.Context, NullLogger<GetLibraryMaterialsPageQueryHandler>.Instance);

    private static GetLibraryMaterialsPageQuery Query(
        Guid topicId,
        int offset,
        int pageSize = 50) =>
        new(
            topicId,
            Search: null,
            LibraryMaterialFilter.All,
            LibraryMaterialSort.Name,
            offset,
            pageSize);
}
