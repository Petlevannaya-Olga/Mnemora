namespace Mnemora.Desktop.ViewModels.Topics;

public sealed class TopicCreatedEventArgs(Guid topicId) : EventArgs
{
    public Guid TopicId { get; } = topicId;
}