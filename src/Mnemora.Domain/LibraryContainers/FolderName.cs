using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.LibraryContainers;

public sealed class FolderName : ValueObject
{
    public const int MINLENGTH = 2;
    public const int MAXLENGTH = 150;

    public string Value { get; }

    private FolderName(string value)
    {
        Value = value;
    }

    public static Result<FolderName, Error> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CommonErrors.IsRequired(nameof(name));
        }

        string normalizedName = name.Trim();

        if (normalizedName.Length is < MINLENGTH or > MAXLENGTH)
        {
            return CommonErrors.LengthIsWrong(
                nameof(name),
                MINLENGTH,
                MAXLENGTH);
        }

        return new FolderName(normalizedName);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
