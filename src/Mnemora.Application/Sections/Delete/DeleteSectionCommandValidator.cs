using FluentValidation;

namespace Mnemora.Application.Sections.Delete;

public sealed class DeleteSectionCommandValidator
    : AbstractValidator<DeleteSectionCommand>
{
    public DeleteSectionCommandValidator()
    {
        RuleFor(command => command.SectionId)
            .NotEmpty()
            .WithMessage("Идентификатор раздела не указан");
    }
}