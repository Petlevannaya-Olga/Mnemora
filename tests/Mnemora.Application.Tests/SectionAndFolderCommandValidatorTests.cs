using Mnemora.Application.Sections.Create;
using Mnemora.Application.Sections.Update;
using Mnemora.Application.LibraryContainers.CreateSectionFolder;
using Mnemora.Application.LibraryContainers.Update;
using Mnemora.Application.LibraryContainers.Delete;
using Mnemora.Domain.Sections;
using Mnemora.Domain.LibraryContainers;
using Xunit;

namespace Mnemora.Application.Tests;

public sealed class SectionAndFolderCommandValidatorTests
{
    [Fact]
    public void CreateSectionFolderValidator_RejectsInvalidAppearance()
    {
        var command = new CreateSectionFolderCommand(Guid.NewGuid(), "Folder", (FolderColor)999, (FolderIcon)999);
        var result = new CreateSectionFolderCommandValidator().Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Color));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Icon));
    }

    [Fact]
    public void UpdateFolderValidator_RejectsInvalidNameAndAppearance()
    {
        var command = new UpdateLibraryFolderCommand(Guid.NewGuid(), " ", (FolderColor)999, (FolderIcon)999);
        var result = new UpdateLibraryFolderCommandValidator().Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "name");
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Color));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Icon));
    }

    [Fact]
    public void UpdateSectionValidator_RejectsInvalidAppearance()
    {
        var command = new UpdateSectionCommand(Guid.NewGuid(), "Section", (SectionColor)999, (SectionIcon)999);
        var result = new UpdateSectionCommandValidator().Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Color));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Icon));
    }

    [Fact]
    public void DeleteFolderValidator_RejectsEmptyId()
    {
        var result = new DeleteLibraryFolderCommandValidator().Validate(new DeleteLibraryFolderCommand(Guid.Empty));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidSectionAndFolderCommands_PassValidation()
    {
        var sectionResult = new CreateSectionCommandValidator().Validate(
            new CreateSectionCommand("Section", SectionColor.Teal, SectionIcon.Folder));
        var folderResult = new CreateSectionFolderCommandValidator().Validate(
            new CreateSectionFolderCommand(Guid.NewGuid(), "Folder", FolderColor.Teal, FolderIcon.Bookmark));
        Assert.True(sectionResult.IsValid);
        Assert.True(folderResult.IsValid);
    }
}
