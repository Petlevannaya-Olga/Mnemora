using Mnemora.Domain.Topics;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Topics.Update;

public sealed record UpdateTopicCommand(
    Guid TopicId,
    string Name,
    TopicColor Color,
    TopicIcon Icon)
    : ICommandValidation;