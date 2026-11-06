using CSharpFunctionalExtensions;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class Question : Material
{
    public MaterialId? ArticleId { get; private set; }

    public override MaterialType Type => MaterialType.Question;

    // EF Core
    private Question()
    {
    }

    private Question(
        LibraryContainerId containerId,
        MaterialTitle title,
        MaterialDifficulty difficulty,
        MaterialIcon icon,
        MaterialExperienceRewards experienceRewards,
        IReadOnlyCollection<MaterialTag> tags,
        MaterialId? articleId)
        : base(containerId, title, difficulty, icon, experienceRewards, tags)
    {
        ArticleId = articleId;
    }

    public static Result<Question, Error> CreateStandaloneInContainer(
        LibraryContainerId? containerId,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards,
        IEnumerable<MaterialTag?>? tags = null)
    {
        return CreateCore(
            containerId,
            title,
            difficulty,
            icon,
            experienceRewards,
            tags,
            articleId: null);
    }

    public static Result<Question, Error> CreateForArticle(
        Article? article,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards,
        IEnumerable<MaterialTag?>? tags = null)
    {
        if (article is null)
        {
            return CommonErrors.IsRequired(nameof(article));
        }

        // Связанный вопрос не хранит собственных тегов. Теги отображаются
        // через связанную статью и поэтому не копируются в Question.
        var createResult = CreateCore(
            article.ContainerId,
            title,
            difficulty,
            icon,
            experienceRewards,
            Array.Empty<MaterialTag>(),
            article.Id);

        if (createResult.IsFailure)
        {
            return createResult.Error;
        }

        return createResult.Value;
    }

    public UnitResult<Error> AttachToArticle(Article? article)
    {
        if (article is null)
        {
            return CommonErrors.IsRequired(nameof(article));
        }

        if (ArticleId == article.Id)
        {
            var existingArticleLocationResult =
                SynchronizeLocationWithArticle(article);

            if (existingArticleLocationResult.IsFailure)
            {
                return existingArticleLocationResult.Error;
            }

            return ReplaceTags(Array.Empty<MaterialTag>());
        }

        if (ArticleId is not null)
        {
            return MaterialErrors.QuestionAlreadyAttachedToAnotherArticle();
        }

        var locationResult = SynchronizeLocationWithArticle(article);

        if (locationResult.IsFailure)
        {
            return locationResult.Error;
        }

        var clearTagsResult = ReplaceTags(Array.Empty<MaterialTag>());

        if (clearTagsResult.IsFailure)
        {
            return clearTagsResult.Error;
        }

        ArticleId = article.Id;
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> DetachFromArticle()
    {
        if (ArticleId is null)
        {
            return UnitResult.Success<Error>();
        }

        // При отвязке вопрос становится самостоятельным без тегов. Старые
        // собственные теги не восстанавливаем и теги статьи не копируем.
        var clearTagsResult = ReplaceTags(Array.Empty<MaterialTag>());

        if (clearTagsResult.IsFailure)
        {
            return clearTagsResult.Error;
        }

        ArticleId = null;
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> MoveToContainer(LibraryContainerId? containerId)
    {
        if (ArticleId is not null)
        {
            return MaterialErrors.AttachedQuestionContainerCannotBeChanged();
        }

        return ChangeContainerCore(containerId);
    }

    public UnitResult<Error> MoveToContainerWithArticle(Article? article)
    {
        if (article is null)
        {
            return CommonErrors.IsRequired(nameof(article));
        }

        if (ArticleId != article.Id)
        {
            return MaterialErrors.QuestionIsNotAttachedToArticle();
        }

        return SynchronizeLocationWithArticle(article);
    }

    public UnitResult<Error> ResetIcon()
    {
        return ChangeIcon(MaterialIcon.DefaultQuestion);
    }

    public UnitResult<Error> StartNewLearningRevision()
    {
        return StartNewLearningRevisionCore();
    }

    private UnitResult<Error> SynchronizeLocationWithArticle(Article article)
    {
        return ChangeContainerCore(article.ContainerId);
    }

    private static Result<Question, Error> CreateCore(
        LibraryContainerId? containerId,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards,
        IEnumerable<MaterialTag?>? tags,
        MaterialId? articleId)
    {
        var actualIcon = icon ?? MaterialIcon.DefaultQuestion;
        var validationResult = ValidateCommonData(
            containerId,
            title,
            difficulty,
            actualIcon,
            experienceRewards);

        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        var tagsResult = PrepareTags(tags);

        if (tagsResult.IsFailure)
        {
            return tagsResult.Error;
        }

        return new Question(
            containerId!,
            title!,
            difficulty!.Value,
            actualIcon,
            experienceRewards!,
            tagsResult.Value,
            articleId);
    }
}
