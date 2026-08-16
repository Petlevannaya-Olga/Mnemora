using FluentValidation;
using Mnemora.Domain.Sections;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Sections.Update;

public sealed class UpdateSectionCommandValidator
    : AbstractValidator<UpdateSectionCommand>
{
    public UpdateSectionCommandValidator()
    {
        RuleFor(command => command.SectionId)
           .MustBeValueObject(SectionId.Create);

        RuleFor(command => command.Name)
            .MustBeValueObject(SectionName.Create);
    }
}