using CSharpFunctionalExtensions;
using Mnemora.Domain.Topics;
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
        TopicId topicId,
        MaterialTitle title,
        MaterialDifficulty difficulty,
        MaterialIcon icon,
        MaterialExperienceRewards experienceRewards,
        IReadOnlyCollection<MaterialTag> tags,
        MaterialId? articleId)
        : base(topicId, title, difficulty, icon, experienceRewards, tags)
    {
        ArticleId = articleId;
    }

    public static Result<Question, Error> CreateStandalone(
        TopicId? topicId,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards,
        IEnumerable<MaterialTag?>? tags = null)
    {
        return CreateCore(topicId, title, difficulty, icon, experienceRewards, tags, articleId: null);
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

        return CreateCore(article.TopicId, title, difficulty, icon, experienceRewards, tags, article.Id);
    }

    public UnitResult<Error> AttachToArticle(Article? article)
    {
        if (article is null)
        {
            return CommonErrors.IsRequired(nameof(article));
        }

        if (ArticleId == article.Id)
        {
            return ChangeTopicCore(article.TopicId);
        }

        if (ArticleId is not null)
        {
            return MaterialErrors.QuestionAlreadyAttachedToAnotherArticle();
        }

        var topicResult = ChangeTopicCore(article.TopicId);

        if (topicResult.IsFailure)
        {
            return topicResult.Error;
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

        ArticleId = null;
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> ChangeTopic(TopicId? topicId)
    {
        if (ArticleId is not null)
        {
            return MaterialErrors.AttachedQuestionTopicCannotBeChanged();
        }

        return ChangeTopicCore(topicId);
    }

    public UnitResult<Error> ChangeTopicWithArticle(Article? article)
    {
        if (article is null)
        {
            return CommonErrors.IsRequired(nameof(article));
        }

        if (ArticleId != article.Id)
        {
            return MaterialErrors.QuestionIsNotAttachedToArticle();
        }

        return ChangeTopicCore(article.TopicId);
    }

    public UnitResult<Error> ResetIcon()
    {
        return ChangeIcon(MaterialIcon.DefaultQuestion);
    }

    public UnitResult<Error> StartNewLearningRevision()
    {
        return StartNewLearningRevisionCore();
    }

    private static Result<Question, Error> CreateCore(
        TopicId? topicId,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards,
        IEnumerable<MaterialTag?>? tags,
        MaterialId? articleId)
    {
        var actualIcon = icon ?? MaterialIcon.DefaultQuestion;
        var validationResult = ValidateCommonData(topicId, title, difficulty, actualIcon, experienceRewards);

        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        var tagsResult = PrepareTags(tags);

        if (tagsResult.IsFailure)
        {
            return tagsResult.Error;
        }

        return new Question(topicId!, title!, difficulty!.Value, actualIcon, experienceRewards!, tagsResult.Value, articleId);
    }
}