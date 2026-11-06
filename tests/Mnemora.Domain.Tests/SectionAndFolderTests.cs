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
        Section section = Section.Create(name, SectionColor.Teal, SectionIcon.Backend);
        DateTime updatedAt = section.UpdatedAt;
        section.Update(name, SectionColor.Teal, SectionIcon.Backend);
        Assert.Equal(updatedAt, section.UpdatedAt);
    }

    [Fact]
    public void Folder_Update_ChangesAppearanceAndName()
    {
        LibraryContainer root = LibraryContainer.CreateRoot(SectionId.New()).Value;
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root, FolderName.Create("Old folder").Value,
            FolderColor.Teal, FolderIcon.Bookmark).Value;
        var result = folder.UpdateFolder(
            FolderName.Create("New folder").Value,
            FolderColor.Purple, FolderIcon.Database);
        Assert.True(result.IsSuccess);
        Assert.Equal("New folder", folder.Name!.Value);
        Assert.Equal(FolderColor.Purple, folder.Color);
        Assert.Equal(FolderIcon.Database, folder.Icon);
    }

    [Fact]
    public void SectionAndFolder_DefaultToUnorderedAndRejectNegativeOrder()
    {
        Section section = Section.Create(
            SectionName.Create("Section").Value, SectionColor.Teal, SectionIcon.Folder);
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root, FolderName.Create("Folder").Value,
            FolderColor.Teal, FolderIcon.Bookmark).Value;
        Assert.Equal(Section.DefaultDisplayOrder, section.DisplayOrder);
        Assert.Equal(LibraryContainer.DefaultDisplayOrder, folder.DisplayOrder);
        Assert.Throws<ArgumentOutOfRangeException>(() => section.ChangeDisplayOrder(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => folder.ChangeDisplayOrder(-1));
    }
}
