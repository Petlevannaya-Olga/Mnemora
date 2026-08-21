using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Materials.Content;
using Mnemora.Domain.Materials;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class MarkdownMaterialContentStoreTests
{
    [Fact]
    public async Task Article_CreateAndRead_RoundTripsMarkdownWithoutTempDirectories()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        IMaterialContentStore store = provider.GetRequiredService<IMaterialContentStore>();
        MaterialId materialId = MaterialId.New();
        ArticleContent content = ArticleContent.Create("# Article\n\nBody").Value;

        var createResult = await store.CreateArticleAsync(
            materialId,
            content,
            CancellationToken.None);
        var readResult = await store.ReadArticleAsync(
            materialId,
            CancellationToken.None);

        Assert.True(createResult.IsSuccess);
        Assert.True(readResult.IsSuccess);
        Assert.Equal(content, readResult.Value);

        string articlesDirectory = System.IO.Path.Combine(
            temporaryDirectory.Path,
            "materials",
            "articles");
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(articlesDirectory),
            path => (System.IO.Path.GetFileName(path) ?? string.Empty)
                .StartsWith('.'));
    }

    [Fact]
    public async Task Question_CreateAndRead_PersistsPromptAndAnswerSeparately()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        IMaterialContentStore store = provider.GetRequiredService<IMaterialContentStore>();
        MaterialId materialId = MaterialId.New();
        QuestionContent content = QuestionContent.Create("Question?", "Answer").Value;

        var createResult = await store.CreateQuestionAsync(
            materialId,
            content,
            CancellationToken.None);
        var readResult = await store.ReadQuestionAsync(
            materialId,
            CancellationToken.None);

        Assert.True(createResult.IsSuccess);
        Assert.True(readResult.IsSuccess);
        Assert.Equal(content, readResult.Value);
    }

    [Fact]
    public async Task CreateExistingMaterial_ReturnsConflictAndPreservesOriginalContent()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        IMaterialContentStore store = provider.GetRequiredService<IMaterialContentStore>();
        MaterialId materialId = MaterialId.New();

        var first = await store.CreateArticleAsync(
            materialId,
            ArticleContent.Create("Original").Value,
            CancellationToken.None);
        var second = await store.CreateArticleAsync(
            materialId,
            ArticleContent.Create("Replacement").Value,
            CancellationToken.None);
        var read = await store.ReadArticleAsync(materialId, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("material.content.already.exists", second.Error.Code);
        Assert.Equal("Original", read.Value.BodyMarkdown);
    }

    [Fact]
    public async Task ReadMissingMaterial_ReturnsNotFound()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        IMaterialContentStore store = provider.GetRequiredService<IMaterialContentStore>();

        var result = await store.ReadArticleAsync(
            MaterialId.New(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("material.content.not.found", result.Error.Code);
    }

    [Fact]
    public async Task Delete_IsIdempotentAndRemovesMaterialDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        IMaterialContentStore store = provider.GetRequiredService<IMaterialContentStore>();
        MaterialId materialId = MaterialId.New();
        await store.CreateArticleAsync(
            materialId,
            ArticleContent.Create("Content").Value,
            CancellationToken.None);

        var first = store.Delete(materialId, MaterialType.Article);
        var second = store.Delete(materialId, MaterialType.Article);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(Directory.Exists(System.IO.Path.Combine(
            temporaryDirectory.Path,
            "materials",
            "articles",
            materialId.Value.ToString("N"))));
    }
}
