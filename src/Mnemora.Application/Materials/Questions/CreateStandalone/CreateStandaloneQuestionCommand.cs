using Mnemora.Domain.Materials;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Questions.CreateStandalone;

public sealed record CreateStandaloneQuestionCommand(
    Guid ContainerId,
    string Title,
    MaterialDifficulty Difficulty,
    string? IconKey,
    int StudyPoints,
    int ReviewPoints,
    string PromptMarkdown,
    string ReferenceAnswerMarkdown,
    IReadOnlyCollection<string>? Tags = null)
    : ICommandValidation;