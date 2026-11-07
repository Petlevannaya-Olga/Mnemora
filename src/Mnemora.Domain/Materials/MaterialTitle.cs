using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class MaterialTitle : ValueObject
{
    #pragma warning disable CA1707
    public const int MIN_LENGTH = 2;
    #pragma warning restore CA1707
    #pragma warning disable CA1707
    public const int MAX_LENGTH = 150;
    #pragma warning restore CA1707

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

        if (normalizedTitle.Length is < MIN_LENGTH or > MAX_LENGTH)
        {
            return CommonErrors.LengthIsWrong(
                nameof(title),
                MIN_LENGTH,
                MAX_LENGTH);
        }

        return new MaterialTitle(normalizedTitle);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}