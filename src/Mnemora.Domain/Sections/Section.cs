namespace Mnemora.Domain.Sections;

public sealed class Section
{
    public const int DefaultDisplayOrder = int.MaxValue;

    public SectionId Id { get; private set; } = null!;

    public SectionName Name { get; private set; } = null!;

    public SectionColor Color { get; private set; }

    public SectionIcon Icon { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public int DisplayOrder { get; private set; } = DefaultDisplayOrder;

    // EF Core
    private Section()
    {
    }

    private Section(
        SectionName name,
        SectionColor color,
        SectionIcon icon)
    {
        var now = DateTime.UtcNow;

        Id = SectionId.New();
        Name = name;
        Color = color;
        Icon = icon;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Section Create(
        SectionName name,
        SectionColor color,
        SectionIcon icon)
    {
        return new Section(
            name,
            color,
            icon);
    }

    public void Update(
        SectionName name,
        SectionColor color,
        SectionIcon icon)
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

    public void UpdateName(SectionName name)
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
