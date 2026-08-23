using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Materials.Articles.Create;
using Mnemora.Application.Materials.Articles.Delete;
using Mnemora.Application.Materials.Content;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared.Abstractions;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Materials;

public sealed class ArticleCommandHandlerTests
{
    [Fact]
    public async Task CreateArticle_PersistsDatabaseRowAndMarkdownContent()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        Topic topic = await CreateTopicAsync(host);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<Guid, CreateArticleCommand>>();
        var command = new CreateArticleCommand(
            topic.Id.Value,
            "EF Core tracking",
            MaterialDifficulty.Medium,
            IconKey: null,
            StudyPoints: 50,
            ReviewPoints: 20,
            BodyMarkdown: "# EF Core tracking",
            Tags: ["EF Core", "Database"]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext verificationContext = await factory.CreateDbContextAsync();
        Article article = await verificationContext.Materials
            .OfType<Article>()
            .Include(material => material.Tags)
            .SingleAsync(material => material.Id == MaterialId.Create(result.Value).Value);
        Assert.Equal("EF Core tracking", article.Title.Value);
        Assert.Equal(2, article.Tags.Count);

        IMaterialContentStore contentStore =
            host.Services.GetRequiredService<IMaterialContentStore>();
        var content = await contentStore.ReadArticleAsync(
            article.Id,
            CancellationToken.None);
        Assert.True(content.IsSuccess);
        Assert.Equal("# EF Core tracking", content.Value.BodyMarkdown);
    }

    [Fact]
    public async Task DeleteArticle_WithLinkedQuestions_ReturnsConflictAndPreservesData()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        Topic topic = await CreateTopicAsync(host);
        Article article = Article.Create(
            topic.Id,
            MaterialTitle.Create("Article").Value,
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value).Value;
        Question question = Question.CreateForArticle(
            article,
            MaterialTitle.Create("Question").Value,
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using (MnemoraDbContext dbContext = await factory.CreateDbContextAsync())
        {
            dbContext.Materials.AddRange(article, question);
            await dbContext.SaveChangesAsync();
        }

        IMaterialContentStore contentStore =
            host.Services.GetRequiredService<IMaterialContentStore>();
        await contentStore.CreateArticleAsync(
            article.Id,
            ArticleContent.Create("Article content").Value,
            CancellationToken.None);
        await contentStore.CreateQuestionAsync(
            question.Id,
            QuestionContent.Create("Question", "Answer").Value,
            CancellationToken.None);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<Guid, DeleteArticleCommand>>();
        var result = await handler.Handle(
            new DeleteArticleCommand(article.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error, error => error.Code == "article.delete.has.questions");

        await using MnemoraDbContext verificationContext = await factory.CreateDbContextAsync();
        Assert.True(await verificationContext.Materials
            .OfType<Article>()
            .AnyAsync(material => material.Id == article.Id));
        Question persistedQuestion = await verificationContext.Materials
            .OfType<Question>()
            .SingleAsync(material => material.Id == question.Id);
        Assert.Equal(article.Id, persistedQuestion.ArticleId);

        Assert.True((await contentStore.ReadArticleAsync(
            article.Id,
            CancellationToken.None)).IsSuccess);
        Assert.True((await contentStore.ReadQuestionAsync(
            question.Id,
            CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task DeleteArticle_WithoutLinkedQuestions_DeletesDatabaseRowAndContent()
    {
        await using var host = await ApplicationTestHost.CreateAsync();
        Topic topic = await CreateTopicAsync(host);
        Article article = Article.Create(
            topic.Id,
            MaterialTitle.Create("Article").Value,
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        var factory = host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using (MnemoraDbContext dbContext = await factory.CreateDbContextAsync())
        {
            dbContext.Materials.Add(article);
            await dbContext.SaveChangesAsync();
        }

        IMaterialContentStore contentStore =
            host.Services.GetRequiredService<IMaterialContentStore>();
        await contentStore.CreateArticleAsync(
            article.Id,
            ArticleContent.Create("Article content").Value,
            CancellationToken.None);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<Guid, DeleteArticleCommand>>();
        var result = await handler.Handle(
            new DeleteArticleCommand(article.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using MnemoraDbContext verificationContext = await factory.CreateDbContextAsync();
        Assert.False(await verificationContext.Materials
            .OfType<Article>()
            .AnyAsync(material => material.Id == article.Id));
        Assert.True((await contentStore.ReadArticleAsync(
            article.Id,
            CancellationToken.None)).IsFailure);
    }

    private static async Task<Topic> CreateTopicAsync(ApplicationTestHost host)
    {
        CancellationToken ct = CancellationToken.None;

        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.Teal,
            SectionIcon.Folder);

        LibraryContainer root =
            LibraryContainer.CreateRoot(section.Id).Value;

        Topic topic = Topic.Create(
            section.Id,
            TopicName.Create("Topic").Value,
            TopicColor.Teal,
            TopicIcon.Bookmark);

        LibraryContainer folder =
            LibraryContainer.CreateFolderWithId(
                LibraryContainerId.Create(topic.Id.Value).Value,
                root,
                FolderName.Create(topic.Name.Value).Value,
                Enum.Parse<FolderColor>(topic.Color.ToString()),
                Enum.Parse<FolderIcon>(topic.Icon.ToString())).Value;

        var factory =
            host.Services.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync(ct);

        dbContext.AddRange(
            section,
            root,
            topic,
            folder);

        await dbContext.SaveChangesAsync(ct);

        return topic;
    }
}
