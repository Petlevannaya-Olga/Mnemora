using Mnemora.Domain.Materials;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Questions.Update;

public sealed record UpdateQuestionCommand(
    Guid QuestionId,
    Guid? TopicId,
    Guid? ArticleId,
    string Title,
    MaterialDifficulty Difficulty,
    string? IconKey,
    int StudyPoints,
    int ReviewPoints,
    IReadOnlyCollection<string>? Tags = null) : ICommandValidation;