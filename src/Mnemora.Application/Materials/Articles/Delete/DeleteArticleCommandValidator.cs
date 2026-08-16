using FluentValidation;
using Mnemora.Domain.Materials;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Materials.Articles.Delete;

public sealed class DeleteArticleCommandValidator : AbstractValidator<DeleteArticleCommand>
{
    public DeleteArticleCommandValidator()
    {
        RuleFor(command => command.ArticleId)
            .MustBeValueObject(MaterialId.Create);
    }
}