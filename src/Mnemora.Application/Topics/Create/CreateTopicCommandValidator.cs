using FluentValidation;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Topics.Create;

public sealed class CreateTopicCommandValidator : AbstractValidator<CreateTopicCommand>
{
    public CreateTopicCommandValidator()
    {
        RuleFor(command => command.SectionId)
            .MustBeValueObject(SectionId.Create);

        RuleFor(command => command.Name)
            .MustBeValueObject(TopicName.Create);

        RuleFor(command => command.Color)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "topic.color.invalid",
                "Выбран некорректный цвет темы",
                nameof(CreateTopicCommand.Color)));

        RuleFor(command => command.Icon)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "topic.icon.invalid",
                "Выбрана некорректная иконка темы",
                nameof(CreateTopicCommand.Icon)));
    }
}
