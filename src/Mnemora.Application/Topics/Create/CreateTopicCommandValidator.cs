using FluentValidation;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
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
    }
}