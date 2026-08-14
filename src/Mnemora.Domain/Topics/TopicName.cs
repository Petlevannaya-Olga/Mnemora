using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Topics;

public sealed class TopicName : ValueObject
{
    public const int MINLENGTH = 2;
    public const int MAXLENGTH = 150;

    public string Value { get; }

    private TopicName(string value)
    {
        Value = value;
    }

    public static Result<TopicName, Error> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CommonErrors.IsRequired(nameof(name));
        }

        var normalizedName = name.Trim();

        if (normalizedName.Length is < MINLENGTH or > MAXLENGTH)
        {
            return CommonErrors.LengthIsWrong(
                nameof(name),
                MINLENGTH,
                MAXLENGTH);
        }

        return new TopicName(normalizedName);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}