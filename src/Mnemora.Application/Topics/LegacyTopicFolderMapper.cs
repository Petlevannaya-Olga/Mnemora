using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Topics;

namespace Mnemora.Application.Topics;

internal static class LegacyTopicFolderMapper
{
    public static FolderName ToFolderName(TopicName topicName) =>
        FolderName.Create(topicName.Value).Value;

    public static FolderColor ToFolderColor(TopicColor topicColor) =>
        Enum.Parse<FolderColor>(topicColor.ToString());

    public static FolderIcon ToFolderIcon(TopicIcon topicIcon) =>
        Enum.Parse<FolderIcon>(topicIcon.ToString());
}
