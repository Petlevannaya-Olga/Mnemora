using FluentValidation;
using Mnemora.Domain.Materials;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Materials.Questions.Delete;

public sealed class DeleteQuestionCommandValidator : AbstractValidator<DeleteQuestionCommand>
{
    public DeleteQuestionCommandValidator()
    {
        RuleFor(command => command.QuestionId).MustBeValueObject(MaterialId.Create);
    }
}