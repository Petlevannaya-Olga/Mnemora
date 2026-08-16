using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Materials.Articles.Delete;

public sealed record DeleteArticleCommand(Guid ArticleId) : ICommandValidation;