using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Domain.Tests;

public sealed class SectionAndTopicTests
{
    [Fact]
    public void Section_UpdateWithSameValues_DoesNotChangeTimestamp()
    {
        SectionName name = SectionName.Create("Backend").Value;
        Section section = Section.Create(name, SectionColor.Teal, SectionIcon.Backend);
        DateTime updatedAt = section.UpdatedAt;

        section.Update(name, SectionColor.Teal, SectionIcon.Backend);

        Assert.Equal(updatedAt, section.UpdatedAt);
    }

    [Fact]
    public void Topic_Update_ChangesAppearanceAndName()
    {
        Topic topic = Topic.Create(
            SectionId.New(),
            TopicName.Create("Old topic").Value,
            TopicColor.Teal,
            TopicIcon.Bookmark);

        topic.Update(
            TopicName.Create("New topic").Value,
            TopicColor.Purple,
            TopicIcon.Database);

        Assert.Equal("New topic", topic.Name.Value);
        Assert.Equal(TopicColor.Purple, topic.Color);
        Assert.Equal(TopicIcon.Database, topic.Icon);
    }

    [Fact]
    public void SectionAndTopic_DefaultToUnorderedAndRejectNegativeOrder()
    {
        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.Teal,
            SectionIcon.Folder);
        Topic topic = Topic.Create(
            section.Id,
            TopicName.Create("Topic").Value,
            TopicColor.Teal,
            TopicIcon.Bookmark);

        Assert.Equal(Section.DefaultDisplayOrder, section.DisplayOrder);
        Assert.Equal(Topic.DefaultDisplayOrder, topic.DisplayOrder);
        Assert.Throws<ArgumentOutOfRangeException>(() => section.ChangeDisplayOrder(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => topic.ChangeDisplayOrder(-1));
    }
}
