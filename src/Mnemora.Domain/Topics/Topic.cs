using Mnemora.Domain.Sections;

namespace Mnemora.Domain.Topics;

public sealed class Topic
{
    public const int DefaultDisplayOrder = int.MaxValue;

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
    /// Цвет темы
    /// </summary>
    public TopicColor Color { get; private set; }

    /// <summary>
    /// Иконка темы
    /// </summary>
    public TopicIcon Icon { get; private set; }

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Дата последнего изменения
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Позиция темы внутри раздела.
    /// </summary>
    public int DisplayOrder { get; private set; } = DefaultDisplayOrder;

    // EF Core
    private Topic()
    {
    }

    private Topic(
        SectionId sectionId,
        TopicName name,
        TopicColor color,
        TopicIcon icon)
    {
        var now = DateTime.UtcNow;

        Id = TopicId.New();
        SectionId = sectionId;
        Name = name;
        Color = color;
        Icon = icon;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Topic Create(
        SectionId sectionId,
        TopicName name,
        TopicColor color,
        TopicIcon icon)
    {
        return new Topic(
            sectionId,
            name,
            color,
            icon);
    }

    public void Update(
        TopicName name,
        TopicColor color,
        TopicIcon icon)
    {
        if (Name == name &&
            Color == color &&
            Icon == icon)
        {
            return;
        }

        Name = name;
        Color = color;
        Icon = icon;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateName(TopicName name)
    {
        Update(
            name,
            Color,
            Icon);
    }

    public void ChangeDisplayOrder(int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        DisplayOrder = displayOrder;
    }
}
