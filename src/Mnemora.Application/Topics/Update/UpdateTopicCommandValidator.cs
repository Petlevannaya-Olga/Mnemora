using FluentValidation;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Topics.Update;

public sealed class UpdateTopicCommandValidator
    : AbstractValidator<UpdateTopicCommand>
{
    public UpdateTopicCommandValidator()
    {
        RuleFor(command => command.TopicId)
            .MustBeValueObject(TopicId.Create);

        RuleFor(command => command.Name)
            .MustBeValueObject(TopicName.Create);

        RuleFor(command => command.Color)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "topic.color.invalid",
                "Выбран некорректный цвет темы",
                nameof(UpdateTopicCommand.Color)));

        RuleFor(command => command.Icon)
            .IsInEnum()
            .WithError(CommonErrors.Validation(
                "topic.icon.invalid",
                "Выбрана некорректная иконка темы",
                nameof(UpdateTopicCommand.Icon)));
    }
}
