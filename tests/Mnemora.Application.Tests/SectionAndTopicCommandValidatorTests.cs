using Mnemora.Application.Sections.Create;
using Mnemora.Application.Sections.Update;
using Mnemora.Application.Topics.Create;
using Mnemora.Application.Topics.Update;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class SectionAndTopicCommandValidatorTests
{
    [Fact]
    public void CreateTopicValidator_RejectsInvalidAppearance()
    {
        var command = new CreateTopicCommand(
            Guid.NewGuid(),
            "Topic",
            (TopicColor)999,
            (TopicIcon)999);

        var result = new CreateTopicCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Color));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Icon));
    }

    [Fact]
    public void UpdateTopicValidator_RejectsInvalidNameAndAppearance()
    {
        var command = new UpdateTopicCommand(
            Guid.NewGuid(),
            " ",
            (TopicColor)999,
            (TopicIcon)999);

        var result = new UpdateTopicCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "name");
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Color));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Icon));
    }

    [Fact]
    public void UpdateSectionValidator_RejectsInvalidAppearance()
    {
        var command = new UpdateSectionCommand(
            Guid.NewGuid(),
            "Section",
            (SectionColor)999,
            (SectionIcon)999);

        var result = new UpdateSectionCommandValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Color));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Icon));
    }

    [Fact]
    public void ValidSectionAndTopicCommands_PassValidation()
    {
        var sectionResult = new CreateSectionCommandValidator().Validate(
            new CreateSectionCommand("Section", SectionColor.Teal, SectionIcon.Folder));
        var topicResult = new CreateTopicCommandValidator().Validate(
            new CreateTopicCommand(Guid.NewGuid(), "Topic", TopicColor.Teal, TopicIcon.Bookmark));

        Assert.True(sectionResult.IsValid);
        Assert.True(topicResult.IsValid);
    }
}
