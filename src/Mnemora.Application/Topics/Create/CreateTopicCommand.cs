using Mnemora.Domain.Topics;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Topics.Create;

public sealed record CreateTopicCommand(
    Guid SectionId,
    string Name,
    TopicColor Color,
    TopicIcon Icon)
    : ICommandValidation;