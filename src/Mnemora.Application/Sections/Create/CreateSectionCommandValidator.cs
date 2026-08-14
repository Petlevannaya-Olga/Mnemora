using FluentValidation;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Sections.Create;

public sealed class CreateSectionCommandValidator
    : AbstractValidator<CreateSectionCommand>
{
    public CreateSectionCommandValidator()
    {
        RuleFor(command => command.Name)
            .MustBeValueObject(SectionName.Create);
        
        RuleFor(x => x.Color)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "section.color.invalid",
                "Выбран некорректный цвет раздела",
                nameof(CreateSectionCommand.Color)));

        RuleFor(x => x.Icon)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "section.icon.invalid",
                "Выбрана некорректная иконка раздела",
                nameof(CreateSectionCommand.Icon)));
    }
}