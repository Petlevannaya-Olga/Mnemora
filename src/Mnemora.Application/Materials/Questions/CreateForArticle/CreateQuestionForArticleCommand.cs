using Mnemora.Domain.Materials;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Questions.CreateForArticle;

public sealed record CreateQuestionForArticleCommand(
    Guid ArticleId,
    string Title,
    MaterialDifficulty Difficulty,
    string? IconKey,
    int StudyPoints,
    int ReviewPoints,
    string PromptMarkdown,
    string ReferenceAnswerMarkdown,
    IReadOnlyCollection<string>? Tags = null) : ICommandValidation;