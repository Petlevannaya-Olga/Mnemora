using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Xunit;

namespace Mnemora.Domain.Tests;

public sealed class MaterialTests
{
    [Fact]
    public void Article_Create_UsesDefaultsAndStartsFirstLearningRevision()
    {
        Article article = CreateArticle(LibraryContainerId.New(), icon: null);

        Assert.Equal(MaterialType.ARTICLE, article.Type);
        Assert.Equal(MaterialIcon.DefaultArticle, article.Icon);
        Assert.Equal(1, article.LearningRevision);
        Assert.Equal(Material.DEFAULT_DISPLAY_ORDER, article.DisplayOrder);
    }

    [Fact]
    public void Article_CreateInContainer_UsesContainerId()
    {
        LibraryContainerId containerId = LibraryContainerId.New();

        Article article = CreateArticle(containerId);

        Assert.Equal(containerId, article.ContainerId);
    }

    [Fact]
    public void Article_MoveToContainer_ChangesContainer()
    {
        Article article = CreateArticle(LibraryContainerId.New());
        LibraryContainerId targetContainerId = LibraryContainerId.New();

        var result = article.MoveToContainer(targetContainerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetContainerId, article.ContainerId);
    }

    [Fact]
    public void Article_CreateInContainer_RejectsMissingContainer()
    {
        var result = Article.CreateInContainer(
            null,
            Title("Article"),
            MaterialDifficulty.MEDIUM,
            null,
            Rewards());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Question_CreateStandaloneInContainer_RejectsMissingContainer()
    {
        var result = Question.CreateStandaloneInContainer(
            null,
            Title("Question"),
            MaterialDifficulty.MEDIUM,
            null,
            Rewards());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Article_Create_RejectsDuplicateTagsIgnoringCase()
    {
        var result = Article.CreateInContainer(
            LibraryContainerId.New(),
            Title("Article"),
            MaterialDifficulty.MEDIUM,
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
            LibraryContainerId.New(),
            tags: [Tag("original")]);

        var result = article.ReplaceTags(
            Enumerable.Range(0, Material.MAX_TAGS + 1)
                .Select(index => Tag($"tag-{index}")));

        Assert.True(result.IsFailure);
        Assert.Single(article.Tags);
        Assert.Equal("original", article.Tags.Single().Value);
    }

    [Fact]
    public void ChangeDifficulty_RejectsUndefinedEnumValue()
    {
        Article article = CreateArticle(LibraryContainerId.New());

        var result = article.ChangeDifficulty((MaterialDifficulty)999);

        Assert.True(result.IsFailure);
        Assert.Equal(MaterialDifficulty.MEDIUM, article.Difficulty);
    }

    [Fact]
    public void StartNewLearningRevision_IncrementsRevision()
    {
        Article article = CreateArticle(LibraryContainerId.New());

        article.StartNewLearningRevision();

        Assert.Equal(2, article.LearningRevision);
    }

    [Fact]
    public void AttachToArticle_MovesQuestionToArticleContainerAndClearsTags()
    {
        Article article = CreateArticle(LibraryContainerId.New());
        Question question = CreateStandaloneQuestion(
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
        Article firstArticle = CreateArticle(LibraryContainerId.New());
        Article secondArticle = CreateArticle(LibraryContainerId.New());
        Question question = CreateStandaloneQuestion(LibraryContainerId.New());
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
        Article article = CreateArticle(LibraryContainerId.New());
        Question question = CreateStandaloneQuestion(LibraryContainerId.New());
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
        Article article = CreateArticle(
            LibraryContainerId.New(),
            tags: [Tag("article")]);

        var result = Question.CreateForArticle(
            article,
            Title("Question"),
            MaterialDifficulty.MEDIUM,
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
        Article article = CreateArticle(LibraryContainerId.New());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => article.ChangeDisplayOrder(-1));
    }

    private static Article CreateArticle(
        LibraryContainerId containerId,
        MaterialIcon? icon = null,
        IReadOnlyCollection<MaterialTag>? tags = null) =>
        Article.CreateInContainer(
            containerId,
            Title("Article"),
            MaterialDifficulty.MEDIUM,
            icon,
            Rewards(),
            tags).Value;

    private static Question CreateStandaloneQuestion(
        LibraryContainerId containerId,
        IReadOnlyCollection<MaterialTag>? tags = null) =>
        Question.CreateStandaloneInContainer(
            containerId,
            Title("Question"),
            MaterialDifficulty.MEDIUM,
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
