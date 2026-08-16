using CSharpFunctionalExtensions;
using Mnemora.Domain.Topics;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public abstract class Material
{
    public const int MaxTags = 10;

    private readonly List<MaterialTag> _tags = [];

    public MaterialId Id { get; private set; } = null!;

    public TopicId TopicId { get; private set; } = null!;

    public MaterialTitle Title { get; private set; } = null!;

    public MaterialDifficulty Difficulty { get; private set; }

    public MaterialIcon Icon { get; private set; } = null!;

    public MaterialExperienceRewards ExperienceRewards { get; private set; } = null!;

    public IReadOnlyCollection<MaterialTag> Tags => _tags;

    public int LearningRevision { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public abstract MaterialType Type { get; }

    // EF Core
    protected Material()
    {
    }

    protected Material(
        TopicId topicId,
        MaterialTitle title,
        MaterialDifficulty difficulty,
        MaterialIcon icon,
        MaterialExperienceRewards experienceRewards,
        IReadOnlyCollection<MaterialTag> tags)
    {
        var now = DateTime.UtcNow;

        Id = MaterialId.New();
        TopicId = topicId;
        Title = title;
        Difficulty = difficulty;
        Icon = icon;
        ExperienceRewards = experienceRewards;
        LearningRevision = 1;
        CreatedAt = now;
        UpdatedAt = now;

        _tags.AddRange(tags);
    }

    public UnitResult<Error> ChangeTitle(MaterialTitle? title)
    {
        if (title is null)
        {
            return CommonErrors.IsRequired(nameof(title));
        }

        if (Title == title)
        {
            return UnitResult.Success<Error>();
        }

        Title = title;
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> ChangeDifficulty(MaterialDifficulty? difficulty)
    {
        if (difficulty is null)
        {
            return CommonErrors.IsRequired(nameof(difficulty));
        }

        if (!Enum.IsDefined(difficulty.Value))
        {
            return MaterialErrors.DifficultyIsInvalid(nameof(difficulty));
        }

        if (Difficulty == difficulty.Value)
        {
            return UnitResult.Success<Error>();
        }

        Difficulty = difficulty.Value;
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> ChangeIcon(MaterialIcon? icon)
    {
        if (icon is null)
        {
            return CommonErrors.IsRequired(nameof(icon));
        }

        if (Icon == icon)
        {
            return UnitResult.Success<Error>();
        }

        Icon = icon;
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> ChangeExperienceRewards(MaterialExperienceRewards? experienceRewards)
    {
        if (experienceRewards is null)
        {
            return CommonErrors.IsRequired(nameof(experienceRewards));
        }

        if (ExperienceRewards == experienceRewards)
        {
            return UnitResult.Success<Error>();
        }

        ExperienceRewards = experienceRewards;
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> AddTag(MaterialTag? tag)
    {
        if (tag is null)
        {
            return CommonErrors.IsRequired(nameof(tag));
        }

        if (_tags.Contains(tag))
        {
            return MaterialErrors.TagsContainDuplicates();
        }

        if (_tags.Count >= MaxTags)
        {
            return MaterialErrors.TagsCountIsTooLarge(MaxTags);
        }

        _tags.Add(tag);
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> RemoveTag(MaterialTag? tag)
    {
        if (tag is null)
        {
            return CommonErrors.IsRequired(nameof(tag));
        }

        var existingTag = _tags.FirstOrDefault(existing => existing == tag);

        if (existingTag is null)
        {
            return UnitResult.Success<Error>();
        }

        _tags.Remove(existingTag);
        Touch();

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> ReplaceTags(IEnumerable<MaterialTag?>? tags)
    {
        var tagsResult = PrepareTags(tags);

        if (tagsResult.IsFailure)
        {
            return tagsResult.Error;
        }

        var preparedTags = tagsResult.Value;

        if (_tags.Count == preparedTags.Count && _tags.All(preparedTags.Contains))
        {
            return UnitResult.Success<Error>();
        }

        _tags.Clear();
        _tags.AddRange(preparedTags);
        Touch();

        return UnitResult.Success<Error>();
    }

    protected UnitResult<Error> ChangeTopic(TopicId? topicId)
    {
        if (topicId is null)
        {
            return CommonErrors.IsRequired(nameof(topicId));
        }

        if (TopicId == topicId)
        {
            return UnitResult.Success<Error>();
        }

        TopicId = topicId;
        Touch();

        return UnitResult.Success<Error>();
    }

    protected UnitResult<Error> StartNewLearningRevision()
    {
        LearningRevision++;
        Touch();

        return UnitResult.Success<Error>();
    }

    protected static UnitResult<Error> ValidateCommonData(
        TopicId? topicId,
        MaterialTitle? title,
        MaterialDifficulty? difficulty,
        MaterialIcon? icon,
        MaterialExperienceRewards? experienceRewards)
    {
        if (topicId is null)
        {
            return CommonErrors.IsRequired(nameof(topicId));
        }

        if (title is null)
        {
            return CommonErrors.IsRequired(nameof(title));
        }

        if (difficulty is null)
        {
            return CommonErrors.IsRequired(nameof(difficulty));
        }

        if (!Enum.IsDefined(difficulty.Value))
        {
            return MaterialErrors.DifficultyIsInvalid(nameof(difficulty));
        }

        if (icon is null)
        {
            return CommonErrors.IsRequired(nameof(icon));
        }

        if (experienceRewards is null)
        {
            return CommonErrors.IsRequired(nameof(experienceRewards));
        }

        return UnitResult.Success<Error>();
    }

    protected static Result<List<MaterialTag>, Error> PrepareTags(IEnumerable<MaterialTag?>? tags)
    {
        var preparedTags = new List<MaterialTag>();

        if (tags is null)
        {
            return preparedTags;
        }

        var uniqueTags = new HashSet<MaterialTag>();

        foreach (var tag in tags)
        {
            if (tag is null)
            {
                return CommonErrors.IsRequired(nameof(tag));
            }

            if (!uniqueTags.Add(tag))
            {
                return MaterialErrors.TagsContainDuplicates();
            }

            if (preparedTags.Count >= MaxTags)
            {
                return MaterialErrors.TagsCountIsTooLarge(MaxTags);
            }

            preparedTags.Add(tag);
        }

        return preparedTags;
    }

    protected void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    protected UnitResult<Error> ChangeTopicCore(TopicId? topicId)
    {
        if (topicId is null)
        {
            return CommonErrors.IsRequired(nameof(topicId));
        }

        if (TopicId == topicId)
        {
            return UnitResult.Success<Error>();
        }

        TopicId = topicId;
        Touch();

        return UnitResult.Success<Error>();
    }

    protected UnitResult<Error> StartNewLearningRevisionCore()
    {
        LearningRevision++;
        Touch();

        return UnitResult.Success<Error>();
    }
}