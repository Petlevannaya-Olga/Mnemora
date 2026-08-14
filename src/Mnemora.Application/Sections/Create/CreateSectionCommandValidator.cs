using FluentValidation;
using Mnemora.Domain.Sections;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Sections.Create;

public sealed class CreateSectionCommandValidator
    : AbstractValidator<CreateSectionCommand>
{
    public CreateSectionCommandValidator()
    {
        RuleFor(command => command.Name)
            .MustBeValueObject(SectionName.Create);
    }
}