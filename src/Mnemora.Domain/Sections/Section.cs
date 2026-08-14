namespace Mnemora.Domain.Sections;

public sealed class Section
{
    public SectionId Id { get; private set; } = null!;

    public SectionName Name { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    // EF Core
    private Section()
    {
    }

    private Section(SectionName name)
    {
        DateTime now = DateTime.UtcNow;

        Id = new SectionId(Guid.NewGuid());

        Name = name;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Section Create(SectionName name)
    {
        return new Section(name);
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