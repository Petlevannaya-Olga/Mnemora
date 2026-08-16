using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class MaterialTitle : ValueObject
{
    public const int MinLength = 2;
    public const int MaxLength = 150;

    public string Value { get; }

    private MaterialTitle(string value)
    {
        Value = value;
    }

    public static Result<MaterialTitle, Error> Create(string? title)
    {
        if (title is null)
        {
            return CommonErrors.IsRequired(nameof(title));
        }

        var normalizedTitle = title.Trim();

        if (normalizedTitle.Length == 0)
        {
            return CommonErrors.IsEmpty(nameof(title));
        }

        if (normalizedTitle.Length is < MinLength or > MaxLength)
        {
            return CommonErrors.LengthIsWrong(
                nameof(title),
                MinLength,
                MaxLength);
        }

        return new MaterialTitle(normalizedTitle);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}