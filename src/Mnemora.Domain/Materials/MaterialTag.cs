using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class MaterialTag : ValueObject
{
    public const int MaxLength = 50;

    private readonly string _normalizedValue;

    public string Value { get; }

    private MaterialTag(string value)
    {
        Value = value;
        _normalizedValue = value.ToUpperInvariant();
    }

    public static Result<MaterialTag, Error> Create(string? tag)
    {
        if (tag is null)
        {
            return CommonErrors.IsRequired(nameof(tag));
        }

        var normalizedTag = tag.Trim();

        if (normalizedTag.Length == 0)
        {
            return CommonErrors.IsEmpty(nameof(tag));
        }

        if (normalizedTag.Length > MaxLength)
        {
            return CommonErrors.LengthIsTooLarge(nameof(tag), MaxLength);
        }

        return new MaterialTag(normalizedTag);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return _normalizedValue;
    }
}