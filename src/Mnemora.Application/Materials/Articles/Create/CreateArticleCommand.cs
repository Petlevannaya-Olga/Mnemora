using Mnemora.Domain.Materials;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Articles.Create;

public sealed record CreateArticleCommand(
    Guid TopicId,
    string Title,
    MaterialDifficulty Difficulty,
    string? IconKey,
    int StudyPoints,
    int ReviewPoints,
    string BodyMarkdown,
    IReadOnlyCollection<string>? Tags = null)
    : ICommandValidation;