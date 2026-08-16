using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.Topics;

public sealed record TopicId
{
    public Guid Value { get; }

    private TopicId(Guid value)
    {
        Value = value;
    }

    public static TopicId New() => new(Guid.NewGuid());

    public static Result<TopicId, Error> Create(Guid topicId)
    {
        if (topicId == Guid.Empty) return CommonErrors.IsEmpty(nameof(topicId));

        return new TopicId(topicId);
    }
}