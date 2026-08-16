using FluentValidation;
using Mnemora.Domain.Topics;
using Mnemora.Shared.Extensions;

namespace Mnemora.Application.Topics.Update;

public sealed class DeleteTopicCommandValidator
    : AbstractValidator<UpdateTopicCommand>
{
    public DeleteTopicCommandValidator()
    {
        RuleFor(command => command.TopicId)
            .MustBeValueObject(TopicId.Create);
    }
}