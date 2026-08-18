using Mnemora.Domain.Materials;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.CreateGraph;

/// <summary>
/// Финальное подтверждение мастера создания материала.
/// Одна команда описывает весь граф, который должен появиться после нажатия
/// «Создать»: основной материал, новые вопросы и привязки готовых вопросов.
/// </summary>
public sealed record CreateMaterialGraphCommand(
    Guid TopicId,
    MaterialType Type,
    string Title,
    MaterialDifficulty Difficulty,
    string? IconKey,
    int StudyPoints,
    int ReviewPoints,
    string BodyMarkdown,
    string? ReferenceAnswerMarkdown,
    IReadOnlyCollection<string>? Tags = null,
    Guid? ArticleId = null,
    IReadOnlyCollection<Guid>? ExistingQuestionIds = null,
    IReadOnlyCollection<CreateMaterialGraphQuestionDraft>? NewQuestions = null)
    : ICommandValidation;

public sealed record CreateMaterialGraphQuestionDraft(
    string Title,
    MaterialDifficulty Difficulty,
    string? IconKey,
    int StudyPoints,
    int ReviewPoints,
    string PromptMarkdown,
    string ReferenceAnswerMarkdown);
