namespace Mnemora.Domain.Sections;

public sealed class Section
{
    public SectionId Id { get; private set; } = null!;

    public SectionName Name { get; private set; } = null!;
    
    public SectionColor Color { get; private set; }

    public SectionIcon Icon { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    // EF Core
    private Section()
    {
    }

    private Section(SectionName name, SectionColor color, SectionIcon icon)
    {
        var now = DateTime.UtcNow;

        Id = new SectionId(Guid.NewGuid());
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
        return new Section(name, color, icon);
    }

    public void UpdateName(SectionName name)
    {
        if (Name == name)
        {
            return;
        }

        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }
}