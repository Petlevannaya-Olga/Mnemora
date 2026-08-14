using FluentValidation;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Topics.Create;

public sealed class CreateTopicCommandValidator : AbstractValidator<CreateTopicCommand>
{
    public CreateTopicCommandValidator()
    {
        RuleFor(command => command.SectionId)
            .NotEmpty()
            .WithError(
                CommonErrors.IsRequired(nameof(CreateTopicCommand.SectionId)));

        RuleFor(command => command.Name)
            .MustBeValueObject(TopicName.Create);
    }
}