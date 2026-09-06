using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Domain.Tests;

public sealed class MaterialTests
{
    [Fact]
    public void Article_Create_UsesDefaultsAndStartsFirstLearningRevision()
    {
        Article article = CreateArticle(TopicId.New(), icon: null);

        Assert.Equal(MaterialType.Article, article.Type);
        Assert.Equal(MaterialIcon.DefaultArticle, article.Icon);
        Assert.Equal(1, article.LearningRevision);
        Assert.Equal(Material.DefaultDisplayOrder, article.DisplayOrder);
    }

    [Fact]
    public void Article_CreateInContainer_UsesContainerId()
    {
        LibraryContainerId containerId = LibraryContainerId.New();

        Article article = CreateArticleInContainer(containerId);

        Assert.Equal(containerId, article.ContainerId);
    }

    [Fact]
    public void Article_MoveToContainer_ChangesContainer()
    {
        Article article = CreateArticleInContainer(LibraryContainerId.New());
        LibraryContainerId targetContainerId = LibraryContainerId.New();

        var result = article.MoveToContainer(targetContainerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetContainerId, article.ContainerId);
    }

    [Fact]
    public void Article_MoveToContainer_DoesNotRewriteLegacyTopicBridge()
    {
        TopicId topicId = TopicId.New();
        Article article = CreateArticle(topicId);
        LibraryContainerId targetContainerId = LibraryContainerId.New();

        var result = article.MoveToContainer(targetContainerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(topicId, article.TopicId);
        Assert.Equal(targetContainerId, article.ContainerId);
    }

    [Fact]
    public void Article_ChangeTopic_SynchronizesContainerBridge()
    {
        Article article = CreateArticle(TopicId.New());
        TopicId targetTopicId = TopicId.New();

        var result = article.ChangeTopic(targetTopicId);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetTopicId, article.TopicId);
        Assert.Equal(targetTopicId.Value, article.ContainerId.Value);
    }

    [Fact]
    public void Article_Create_RejectsDuplicateTagsIgnoringCase()
    {
        var result = Article.Create(
            TopicId.New(),
            Title("Article"),
            MaterialDifficulty.Medium,
            MaterialIcon.DefaultArticle,
            Rewards(),
            [Tag("EF"), Tag("ef")]);

        Assert.True(result.IsFailure);
        Assert.Equal("material.tags.contain.duplicates", result.Error.Code);
    }

    [Fact]
    public void ReplaceTags_RejectsMoreThanTenTagsAndPreservesExistingTags()
    {
        Article article = CreateArticle(
            TopicId.New(),
            tags: [Tag("original")]);

        var result = article.ReplaceTags(
            Enumerable.Range(0, Material.MaxTags + 1)
                .Select(index => Tag($"tag-{index}")));

        Assert.True(result.IsFailure);
        Assert.Single(article.Tags);
        Assert.Equal("original", article.Tags.Single().Value);
    }

    [Fact]
    public void ChangeDifficulty_RejectsUndefinedEnumValue()
    {
        Article article = CreateArticle(TopicId.New());

        var result = article.ChangeDifficulty((MaterialDifficulty)999);

        Assert.True(result.IsFailure);
        Assert.Equal(MaterialDifficulty.Medium, article.Difficulty);
    }

    [Fact]
    public void StartNewLearningRevision_IncrementsRevision()
    {
        Article article = CreateArticle(TopicId.New());

        article.StartNewLearningRevision();

        Assert.Equal(2, article.LearningRevision);
    }

    [Fact]
    public void AttachToArticle_MovesQuestionToArticleContainerAndClearsTags()
    {
        Article article = CreateArticleInContainer(LibraryContainerId.New());
        Question question = CreateStandaloneQuestionInContainer(
            LibraryContainerId.New(),
            [Tag("standalone")]);

        var result = question.AttachToArticle(article);

        Assert.True(result.IsSuccess);
        Assert.Equal(article.Id, question.ArticleId);
        Assert.Equal(article.ContainerId, question.ContainerId);
        Assert.Empty(question.Tags);
    }

    [Fact]
    public void AttachedQuestion_CannotMoveIndependentlyOrAttachToAnotherArticle()
    {
        Article firstArticle = CreateArticleInContainer(LibraryContainerId.New());
        Article secondArticle = CreateArticleInContainer(LibraryContainerId.New());
        Question question = CreateStandaloneQuestionInContainer(LibraryContainerId.New());
        question.AttachToArticle(firstArticle);

        var moveResult = question.MoveToContainer(LibraryContainerId.New());
        var attachResult = question.AttachToArticle(secondArticle);

        Assert.True(moveResult.IsFailure);
        Assert.True(attachResult.IsFailure);
        Assert.Equal(firstArticle.Id, question.ArticleId);
        Assert.Equal(firstArticle.ContainerId, question.ContainerId);
    }

    [Fact]
    public void DetachFromArticle_MakesQuestionStandaloneAgain()
    {
        Article article = CreateArticleInContainer(LibraryContainerId.New());
        Question question = CreateStandaloneQuestionInContainer(LibraryContainerId.New());
        question.AttachToArticle(article);
        LibraryContainerId newContainerId = LibraryContainerId.New();

        var detachResult = question.DetachFromArticle();
        var moveResult = question.MoveToContainer(newContainerId);

        Assert.True(detachResult.IsSuccess);
        Assert.True(moveResult.IsSuccess);
        Assert.Null(question.ArticleId);
        Assert.Equal(newContainerId, question.ContainerId);
    }

    [Fact]
    public void CreateForArticle_DoesNotCopyQuestionTagsAndUsesArticleContainer()
    {
        Article article = CreateArticleInContainer(
            LibraryContainerId.New(),
            tags: [Tag("article")]);

        var result = Question.CreateForArticle(
            article,
            Title("Question"),
            MaterialDifficulty.Medium,
            null,
            Rewards(),
            [Tag("question")]);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Tags);
        Assert.Equal(article.Id, result.Value.ArticleId);
        Assert.Equal(article.ContainerId, result.Value.ContainerId);
    }

    [Fact]
    public void ChangeDisplayOrder_RejectsNegativeValue()
    {
        Article article = CreateArticle(TopicId.New());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => article.ChangeDisplayOrder(-1));
    }

    private static Article CreateArticle(
        TopicId topicId,
        MaterialIcon? icon = null,
        IReadOnlyCollection<MaterialTag>? tags = null) =>
        Article.Create(
            topicId,
            Title("Article"),
            MaterialDifficulty.Medium,
            icon,
            Rewards(),
            tags).Value;

    private static Article CreateArticleInContainer(
        LibraryContainerId containerId,
        MaterialIcon? icon = null,
        IReadOnlyCollection<MaterialTag>? tags = null) =>
        Article.CreateInContainer(
            containerId,
            Title("Article"),
            MaterialDifficulty.Medium,
            icon,
            Rewards(),
            tags).Value;

    private static Question CreateStandaloneQuestion(
        TopicId topicId,
        IReadOnlyCollection<MaterialTag>? tags = null) =>
        Question.CreateStandalone(
            topicId,
            Title("Question"),
            MaterialDifficulty.Medium,
            null,
            Rewards(),
            tags).Value;

    private static Question CreateStandaloneQuestionInContainer(
        LibraryContainerId containerId,
        IReadOnlyCollection<MaterialTag>? tags = null) =>
        Question.CreateStandaloneInContainer(
            containerId,
            Title("Question"),
            MaterialDifficulty.Medium,
            null,
            Rewards(),
            tags).Value;

    private static MaterialTitle Title(string value) =>
        MaterialTitle.Create(value).Value;

    private static MaterialTag Tag(string value) =>
        MaterialTag.Create(value).Value;

    private static MaterialExperienceRewards Rewards() =>
        MaterialExperienceRewards.Create(50, 20).Value;
}
