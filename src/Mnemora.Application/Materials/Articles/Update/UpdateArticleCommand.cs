using Mnemora.Domain.Materials;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Articles.Update;

public sealed record UpdateArticleCommand(
    Guid ArticleId,
    Guid ContainerId,
    string Title,
    MaterialDifficulty Difficulty,
    string? IconKey,
    int StudyPoints,
    int ReviewPoints,
    IReadOnlyCollection<string>? Tags = null) : ICommandValidation;