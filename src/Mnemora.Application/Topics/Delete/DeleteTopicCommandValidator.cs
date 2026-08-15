using FluentValidation;

namespace Mnemora.Application.Topics.Delete;

public sealed class DeleteTopicCommandValidator
    : AbstractValidator<DeleteTopicCommand>
{
    public DeleteTopicCommandValidator()
    {
        RuleFor(command => command.TopicId)
            .NotEmpty()
            .WithMessage("Идентификатор темы не указан");
    }
}