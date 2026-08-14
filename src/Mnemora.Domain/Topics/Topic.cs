using Mnemora.Domain.Sections;

namespace Mnemora.Domain.Topics;

public sealed class Topic
{
    /// <summary>
    /// Идентификатор темы
    /// </summary>
    public TopicId Id { get; private set; } = null!;

    /// <summary>
    /// Раздел, которому принадлежит тема
    /// </summary>
    public SectionId SectionId { get; private set; } = null!;

    /// <summary>
    /// Название темы
    /// </summary>
    public TopicName Name { get; private set; } = null!;

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Дата последнего изменения
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    // EF Core
    private Topic()
    {
    }

    private Topic(SectionId sectionId, TopicName name)
    {
        var now = DateTime.UtcNow;

        Id = new TopicId(Guid.NewGuid());
        SectionId = sectionId;
        Name = name;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Topic Create(SectionId sectionId, TopicName name)
    {
        return new Topic(sectionId, name);
    }

    public void UpdateName(TopicName name)
    {
        if (Name == name)
        {
            return;
        }

        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }
}