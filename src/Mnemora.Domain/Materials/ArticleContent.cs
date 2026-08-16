using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class ArticleContent : ValueObject
{
    public string BodyMarkdown { get; }

    private ArticleContent(string bodyMarkdown)
    {
        BodyMarkdown = bodyMarkdown;
    }

    public static Result<ArticleContent, Error> Create(string? bodyMarkdown)
    {
        if (bodyMarkdown is null)
        {
            return CommonErrors.IsRequired(nameof(bodyMarkdown));
        }

        if (string.IsNullOrWhiteSpace(bodyMarkdown))
        {
            return CommonErrors.IsEmpty(nameof(bodyMarkdown));
        }

        return new ArticleContent(bodyMarkdown);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return BodyMarkdown;
    }
}