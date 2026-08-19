using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Mnemora.Domain.Materials;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class LibraryPagingModelTests
{
    [Fact]
    public async Task Model_ContainsIndexesRequiredByPagedLibraryQueries()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        IEntityType material = db.Context.Model.FindEntityType(typeof(Material))
                               ?? throw new InvalidOperationException("Material mapping not found.");
        IEntityType question = db.Context.Model.FindEntityType(typeof(Question))
                               ?? throw new InvalidOperationException("Question mapping not found.");

        string[][] materialIndexes = material.GetIndexes()
            .Select(index => index.Properties.Select(property => property.Name).ToArray())
            .ToArray();

        materialIndexes.Should().Contain(index =>
            index.SequenceEqual(new[] { nameof(Material.TopicId), nameof(Material.DisplayOrder), nameof(Material.Id) }));
        materialIndexes.Should().Contain(index =>
            index.SequenceEqual(new[] { nameof(Material.TopicId), nameof(Material.UpdatedAt), nameof(Material.Id) }));
        materialIndexes.Should().Contain(index =>
            index.SequenceEqual(new[] { nameof(Material.TopicId), nameof(Material.CreatedAt), nameof(Material.Id) }));
        materialIndexes.Should().Contain(index =>
            index.SequenceEqual(new[] { nameof(Material.TopicId), nameof(Material.Title), nameof(Material.Id) }));

        question.GetIndexes()
            .Should().Contain(index => index.Properties.Count == 1 &&
                                       index.Properties[0].Name == nameof(Question.ArticleId));
        question.GetIndexes()
            .Should().Contain(index => index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Material.TopicId), nameof(Question.ArticleId) }));
    }
}
