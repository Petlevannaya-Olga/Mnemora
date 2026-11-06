using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Materials.Articles.Create;
using Mnemora.Application.Materials.Articles.Update;
using Mnemora.Application.Materials.Content;
using Mnemora.Application.Materials.Questions.CreateStandalone;
using Mnemora.Application.Materials.Questions.Update;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared.Abstractions;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Materials;

/// <summary>
/// Regression tests: materials in the section root and nested folders must not
/// require a corresponding row in the obsolete topics table.
/// </summary>
public sealed class MaterialContainerCommandsTests
{
    [Fact]
    public async Task CreateArticle_InSectionRootWithoutTopic_PersistsContentAndContainer()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        var (rootId, _) = await SeedContainersAsync(host);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<Guid, CreateArticleCommand>>();

        var result = await handler.Handle(
            new CreateArticleCommand(rootId, "Root article", MaterialDifficulty.Medium,
                null, 50, 20, "# In root"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Article article = await db.Materials.OfType<Article>()
            .SingleAsync(a => a.Id == MaterialId.Create(result.Value).Value);
        Assert.Equal(rootId, article.ContainerId.Value);
        Assert.False(await db.Topics.AnyAsync());

        var store = host.Services.GetRequiredService<IMaterialContentStore>();
        var content = await store.ReadArticleAsync(article.Id, CancellationToken.None);
        Assert.True(content.IsSuccess);
        Assert.Equal("# In root", content.Value.BodyMarkdown);
    }

    [Fact]
    public async Task CreateStandaloneQuestion_InNestedFolderWithoutTopic_PersistsContainer()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        var (_, folderId) = await SeedContainersAsync(host);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<Guid, CreateStandaloneQuestionCommand>>();

        var result = await handler.Handle(
            new CreateStandaloneQuestionCommand(folderId, "Question", MaterialDifficulty.Easy,
                null, 50, 20, "Prompt", "Answer"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Question question = await db.Materials.OfType<Question>()
            .SingleAsync(q => q.Id == MaterialId.Create(result.Value).Value);
        Assert.Equal(folderId, question.ContainerId.Value);
        Assert.Null(question.ArticleId);
        Assert.False(await db.Topics.AnyAsync());
    }

    [Fact]
    public async Task MoveArticle_ToNestedFolder_MovesLinkedQuestion()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        var (rootId, folderId) = await SeedContainersAsync(host);
        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        Article article = Article.CreateInContainer(
            LibraryContainerId.Create(rootId).Value,
            MaterialTitle.Create("Original article").Value,
            MaterialDifficulty.Medium, MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value).Value;
        Question question = Question.CreateForArticle(
            article, MaterialTitle.Create("Linked question").Value,
            MaterialDifficulty.Medium, MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Materials.AddRange(article, question);
            await db.SaveChangesAsync();
        }

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<Guid, UpdateArticleCommand>>();
        var result = await handler.Handle(
            new UpdateArticleCommand(article.Id.Value, folderId, "Moved article",
                MaterialDifficulty.Medium, null, 50, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var verification = await factory.CreateDbContextAsync();
        Article moved = await verification.Materials.OfType<Article>()
            .SingleAsync(a => a.Id == article.Id);
        Question linked = await verification.Materials.OfType<Question>()
            .SingleAsync(q => q.Id == question.Id);
        Assert.Equal(folderId, moved.ContainerId.Value);
        Assert.Equal(folderId, linked.ContainerId.Value);
        Assert.Equal(moved.Id, linked.ArticleId);
    }

    [Fact]
    public async Task UpdateStandaloneQuestion_ToSectionRootWithoutTopic_ChangesContainer()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        var (rootId, folderId) = await SeedContainersAsync(host);
        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        Question question = Question.CreateStandaloneInContainer(
            LibraryContainerId.Create(folderId).Value,
            MaterialTitle.Create("Original question").Value,
            MaterialDifficulty.Easy, MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Materials.Add(question);
            await db.SaveChangesAsync();
        }

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<Guid, UpdateQuestionCommand>>();
        var result = await handler.Handle(
            new UpdateQuestionCommand(question.Id.Value, rootId, null,
                "Moved question", MaterialDifficulty.Medium, null, 50, 20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var verification = await factory.CreateDbContextAsync();
        Question moved = await verification.Materials.OfType<Question>()
            .SingleAsync(q => q.Id == question.Id);
        Assert.Equal(rootId, moved.ContainerId.Value);
        Assert.Equal("Moved question", moved.Title.Value);
        Assert.Null(moved.ArticleId);
    }

    [Fact]
    public async Task UpdateQuestion_AttachToArticleInNestedFolder_UsesArticleContainer()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        var (rootId, folderId) = await SeedContainersAsync(host);
        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        Article article = Article.CreateInContainer(
            LibraryContainerId.Create(folderId).Value,
            MaterialTitle.Create("Parent article").Value,
            MaterialDifficulty.Medium, MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value).Value;
        Question question = Question.CreateStandaloneInContainer(
            LibraryContainerId.Create(rootId).Value,
            MaterialTitle.Create("Standalone").Value,
            MaterialDifficulty.Easy, MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Materials.AddRange(article, question);
            await db.SaveChangesAsync();
        }

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<Guid, UpdateQuestionCommand>>();
        var result = await handler.Handle(
            new UpdateQuestionCommand(question.Id.Value, null, article.Id.Value,
                "Attached", MaterialDifficulty.Medium, null, 50, 20),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var verification = await factory.CreateDbContextAsync();
        Question attached = await verification.Materials.OfType<Question>()
            .SingleAsync(q => q.Id == question.Id);
        Assert.Equal(folderId, attached.ContainerId.Value);
        Assert.Equal(article.Id, attached.ArticleId);
    }

    [Fact]
    public async Task CreateArticle_WithMissingContainer_ReturnsNotFoundWithoutWritingMaterial()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            ICommandHandler<Guid, CreateArticleCommand>>();
        var result = await handler.Handle(
            new CreateArticleCommand(Guid.NewGuid(), "Orphan", MaterialDifficulty.Easy,
                null, 50, 20, "No parent"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.Code == "container.not.found");
        await using var db = await factory.CreateDbContextAsync();
        Assert.False(await db.Materials.AnyAsync());
    }

    private static async Task<(Guid RootId, Guid NestedId)> SeedContainersAsync(
        ApplicationTestHost host)
    {
        Section section = Section.Create(
            SectionName.Create("Materials section").Value,
            SectionColor.Teal, SectionIcon.Folder);
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer parent = LibraryContainer.CreateFolder(
            root, FolderName.Create("Backend").Value,
            FolderColor.Teal, FolderIcon.Folder).Value;
        LibraryContainer nested = LibraryContainer.CreateFolder(
            parent, FolderName.Create("CSharp").Value,
            FolderColor.Teal, FolderIcon.Folder).Value;

        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        db.AddRange(section, root, parent, nested);
        await db.SaveChangesAsync();
        return (root.Id.Value, nested.Id.Value);
    }
}
