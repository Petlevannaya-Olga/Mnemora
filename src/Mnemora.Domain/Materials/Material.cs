using CSharpFunctionalExtensions;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Topics;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public abstract class Material
{
    public const int MaxTags = 10;
    public const int DefaultDisplayOrder = int.MaxValue;

    private readonly List<MaterialTag> _tags = [];

    public MaterialId Id { get; private set; } = null!;

    // Переходное поле для старого Application. Удалим его после полного
    // перевода запросов и команд с Topic на LibraryContainer.
    public TopicId TopicId { get; private set; } = null!;

    // Новый источник расположения материала.
    public LibraryContainerId ContainerId { get; private set; } = null!;

    public MaterialTitle Title { get; private set; } = null!;

    public MaterialDifficulty Difficulty { get; private set; }

    public MaterialIcon Icon { get; private set; } = null!;

    public MaterialExperienceRewards ExperienceRewards { get; private set; } = null!;

    public IReadOnlyCollection<MaterialTag> Tags => _tags;

    public int LearningRevision { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public int DisplayOrder { get; private set; } = DefaultDisplayOrder;

    public abstract MaterialType Type { get; }

    public void ChangeDisplayOrder(int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        DisplayOrder = displayOrder;
    }

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
        ContainerId = LibraryContainerId.Create(topicId.Value).Value;
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

    protected UnitResult<Error> ChangeContainerCore(LibraryContainerId? containerId)
    {
        if (containerId is null)
        {
            return CommonErrors.IsRequired(nameof(containerId));
        }

        if (ContainerId == containerId)
        {
            return UnitResult.Success<Error>();
        }

        ContainerId = containerId;
        Touch();

        return UnitResult.Success<Error>();
    }

    protected UnitResult<Error> ChangeTopicCore(TopicId? topicId)
    {
        if (topicId is null)
        {
            return CommonErrors.IsRequired(nameof(topicId));
        }

        LibraryContainerId containerId =
            LibraryContainerId.Create(topicId.Value).Value;

        if (TopicId == topicId &&
            ContainerId == containerId)
        {
            return UnitResult.Success<Error>();
        }

        // Старые команды ещё меняют Topic. Пока они существуют, считаем это
        // перемещением в соответствующую папку первого уровня и синхронно
        // обновляем новый источник расположения.
        TopicId = topicId;
        ContainerId = containerId;
        Touch();

        return UnitResult.Success<Error>();
    }

    protected UnitResult<Error> StartNewLearningRevisionCore()
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
}
