using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Sections;

public sealed record SectionId
{
    public Guid Value { get; }

    private SectionId(Guid value)
    {
        Value = value;
    }

    public static SectionId New() => new(Guid.NewGuid());

    public static Result<SectionId, Error> Create(Guid sectionId)
    {
        if (sectionId == Guid.Empty) return CommonErrors.IsEmpty(nameof(sectionId));

        return new SectionId(sectionId);
    }
}