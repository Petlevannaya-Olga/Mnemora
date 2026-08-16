using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class MaterialIcon : ValueObject
{
    public const int MaxKeyLength = 50;

    public static MaterialIcon DefaultArticle { get; } = new("article");

    public static MaterialIcon DefaultQuestion { get; } = new("question");

    public string Key { get; }

    private MaterialIcon(string key)
    {
        Key = key;
    }

    public static Result<MaterialIcon, Error> Create(string? key)
    {
        if (key is null)
        {
            return CommonErrors.IsRequired(nameof(key));
        }

        var normalizedKey = key.Trim().ToLowerInvariant();

        if (normalizedKey.Length == 0)
        {
            return CommonErrors.IsEmpty(nameof(key));
        }

        if (normalizedKey.Length > MaxKeyLength)
        {
            return CommonErrors.LengthIsTooLarge(nameof(key), MaxKeyLength);
        }

        if (!IsValidKey(normalizedKey))
        {
            return MaterialErrors.IconKeyIsInvalid(nameof(key));
        }

        return new MaterialIcon(normalizedKey);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Key;
    }

    private static bool IsValidKey(string key)
    {
        if (!char.IsAsciiLetter(key[0]))
        {
            return false;
        }

        return key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }
}