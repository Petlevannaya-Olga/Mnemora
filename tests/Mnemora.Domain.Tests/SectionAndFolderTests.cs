using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Domain.Tests;

public sealed class SectionAndFolderTests
{
    [Fact]
    public void Section_UpdateWithSameValues_DoesNotChangeTimestamp()
    {
        SectionName name = SectionName.Create("Backend").Value;
        Section section = Section.Create(name, SectionColor.TEAL, SectionIcon.BACKEND);
        DateTime updatedAt = section.UpdatedAt;
        section.Update(name, SectionColor.TEAL, SectionIcon.BACKEND);
        Assert.Equal(updatedAt, section.UpdatedAt);
    }

    [Fact]
    public void Folder_Update_ChangesAppearanceAndName()
    {
        LibraryContainer root = LibraryContainer.CreateRoot(SectionId.New()).Value;
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root, FolderName.Create("Old folder").Value,
            FolderColor.TEAL, FolderIcon.BOOKMARK).Value;
        var result = folder.UpdateFolder(
            FolderName.Create("New folder").Value,
            FolderColor.PURPLE, FolderIcon.DATABASE);
        Assert.True(result.IsSuccess);
        Assert.Equal("New folder", folder.Name!.Value);
        Assert.Equal(FolderColor.PURPLE, folder.Color);
        Assert.Equal(FolderIcon.DATABASE, folder.Icon);
    }

    [Fact]
    public void SectionAndFolder_DefaultToUnorderedAndRejectNegativeOrder()
    {
        Section section = Section.Create(
            SectionName.Create("Section").Value, SectionColor.TEAL, SectionIcon.FOLDER);
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root, FolderName.Create("Folder").Value,
            FolderColor.TEAL, FolderIcon.BOOKMARK).Value;
        Assert.Equal(Section.DEFAULT_DISPLAY_ORDER, section.DisplayOrder);
        Assert.Equal(LibraryContainer.DEFAULT_DISPLAY_ORDER, folder.DisplayOrder);
        Assert.Throws<ArgumentOutOfRangeException>(() => section.ChangeDisplayOrder(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => folder.ChangeDisplayOrder(-1));
    }
}
