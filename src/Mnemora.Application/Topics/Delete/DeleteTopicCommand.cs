using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Topics.Delete;

public sealed record DeleteTopicCommand(Guid TopicId) : ICommandValidation;