using FluentValidation;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
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

        RuleFor(command => command.Color)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "section.color.invalid",
                "Выбран некорректный цвет раздела",
                nameof(UpdateSectionCommand.Color)));

        RuleFor(command => command.Icon)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "section.icon.invalid",
                "Выбрана некорректная иконка раздела",
                nameof(UpdateSectionCommand.Icon)));
    }
}
