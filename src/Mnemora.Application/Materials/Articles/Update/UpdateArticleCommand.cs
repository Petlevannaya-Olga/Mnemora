using Mnemora.Domain.Materials;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Articles.Update;

public sealed record UpdateArticleCommand(
    Guid ArticleId,
    Guid TopicId,
    string Title,
    MaterialDifficulty Difficulty,
    string? IconKey,
    int StudyPoints,
    int ReviewPoints,
    IReadOnlyCollection<string>? Tags = null) : ICommandValidation;