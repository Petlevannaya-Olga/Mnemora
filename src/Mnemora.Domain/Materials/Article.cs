using CSharpFunctionalExtensions;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class Article : Material
{
    public override MaterialType Type => MaterialType.ARTICLE;

    // EF Core
    private Article()
    {
    }

    private Article(
        LibraryContainerId containerId,
        MaterialTitle title,
        MaterialDifficulty difficulty,
        MaterialIcon icon,
        MaterialExperienceRewards experienceRewards,
        IReadOnlyCollection<MaterialTag> tags)
        : base(containerId, title, difficulty, icon, experienceRewards, tags)
    {
    }

    public static Result<Article, Error> CreateInContainer(
        LibraryContainerId? containerId,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards,
        IEnumerable<MaterialTag?>? tags = null)
    {
        var actualIcon = icon ?? MaterialIcon.DefaultArticle;
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

        return new Article(
            containerId!,
            title!,
            difficulty!.Value,
            actualIcon,
            experienceRewards!,
            tagsResult.Value);
    }

    public UnitResult<Error> MoveToContainer(LibraryContainerId? containerId)
    {
        return ChangeContainerCore(containerId);
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
