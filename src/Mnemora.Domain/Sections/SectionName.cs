using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Sections;

public sealed class SectionName : ValueObject
{
    public const int MINLENGTH = 2;
    public const int MAXLENGTH = 150;

    public string Value { get; }

    private SectionName(string value)
    {
        Value = value;
    }

    public static Result<SectionName, Error> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CommonErrors.IsRequired(nameof(name));
        }

        string normalizedName = name.Trim();

        if (normalizedName.Length
            is < MINLENGTH or > MAXLENGTH)
        {
            return CommonErrors.LengthIsWrong(
                nameof(name),
                MINLENGTH,
                MAXLENGTH);
        }

        return new SectionName(
            normalizedName);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}