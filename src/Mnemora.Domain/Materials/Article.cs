using CSharpFunctionalExtensions;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Topics;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class Article : Material
{
    public override MaterialType Type => MaterialType.Article;

    // EF Core
    private Article()
    {
    }

    private Article(
        TopicId topicId,
        MaterialTitle title,
        MaterialDifficulty difficulty,
        MaterialIcon icon,
        MaterialExperienceRewards experienceRewards,
        IReadOnlyCollection<MaterialTag> tags)
        : base(topicId, title, difficulty, icon, experienceRewards, tags)
    {
    }

    public static Result<Article, Error> Create(
        TopicId? topicId,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards,
        IEnumerable<MaterialTag?>? tags = null)
    {
        var actualIcon = icon ?? MaterialIcon.DefaultArticle;
        var validationResult = ValidateCommonData(
            topicId,
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

        return new Article(
            topicId!,
            title!,
            difficulty!.Value,
            actualIcon,
            experienceRewards!,
            tagsResult.Value);
    }

    /// <summary>
    /// Переходная фабрика для новой модели библиотеки.
    /// Используется, пока старые application-команды ещё работают с TopicId.
    /// </summary>
    public static Result<Article, Error> CreateInContainer(
        LibraryContainerId? containerId,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards,
        IEnumerable<MaterialTag?>? tags = null)
    {
        if (containerId is null)
        {
            return CommonErrors.IsRequired(nameof(containerId));
        }

        TopicId topicId = TopicId.Create(containerId.Value).Value;

        return Create(
            topicId,
            title,
            difficulty,
            icon,
            experienceRewards,
            tags);
    }

    public UnitResult<Error> MoveToContainer(LibraryContainerId? containerId)
    {
        return ChangeContainerCore(containerId);
    }

    public UnitResult<Error> ChangeTopic(TopicId? topicId)
    {
        return ChangeTopicCore(topicId);
    }

    public UnitResult<Error> ResetIcon()
    {
        return ChangeIcon(MaterialIcon.DefaultArticle);
    }

    public UnitResult<Error> StartNewLearningRevision()
    {
        return StartNewLearningRevisionCore();
    }
}
