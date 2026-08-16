using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Materials;

public sealed class QuestionContent : ValueObject
{
    public string PromptMarkdown { get; }

    public string ReferenceAnswerMarkdown { get; }

    private QuestionContent(string promptMarkdown, string referenceAnswerMarkdown)
    {
        PromptMarkdown = promptMarkdown;
        ReferenceAnswerMarkdown = referenceAnswerMarkdown;
    }

    public static Result<QuestionContent, Error> Create(string? promptMarkdown, string? referenceAnswerMarkdown)
    {
        if (promptMarkdown is null)
        {
            return CommonErrors.IsRequired(nameof(promptMarkdown));
        }

        if (string.IsNullOrWhiteSpace(promptMarkdown))
        {
            return CommonErrors.IsEmpty(nameof(promptMarkdown));
        }

        if (referenceAnswerMarkdown is null)
        {
            return CommonErrors.IsRequired(nameof(referenceAnswerMarkdown));
        }

        if (string.IsNullOrWhiteSpace(referenceAnswerMarkdown))
        {
            return CommonErrors.IsEmpty(nameof(referenceAnswerMarkdown));
        }

        return new QuestionContent(promptMarkdown, referenceAnswerMarkdown);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return PromptMarkdown;
        yield return ReferenceAnswerMarkdown;
    }
}