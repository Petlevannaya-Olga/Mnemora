using FluentValidation;
using Mnemora.Domain.Sections;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Sections.Delete;

public sealed class DeleteSectionCommandValidator
    : AbstractValidator<DeleteSectionCommand>
{
    public DeleteSectionCommandValidator()
    {
        RuleFor(command => command.SectionId)
            .MustBeValueObject(SectionId.Create);
    }
}