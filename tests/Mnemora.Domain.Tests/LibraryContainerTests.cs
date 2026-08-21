using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Domain.Tests;

public sealed class LibraryContainerTests
{
    [Fact]
    public void CreateRoot_CreatesInvisibleRootForSection()
    {
        SectionId sectionId = SectionId.New();

        var result = LibraryContainer.CreateRoot(sectionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(sectionId, result.Value.SectionId);
        Assert.True(result.Value.IsRoot);
        Assert.False(result.Value.IsFolder);
        Assert.Null(result.Value.ParentId);
        Assert.Equal(LibraryContainer.RootDepth, result.Value.Depth);
        Assert.Null(result.Value.Name);
        Assert.Null(result.Value.Color);
        Assert.Null(result.Value.Icon);
    }

    [Fact]
    public void CreateFolder_InheritsSectionAndUsesParentContainer()
    {
        LibraryContainer root = CreateRoot();

        var result = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("CLR").Value,
            FolderColor.Teal,
            FolderIcon.DotNet);

        Assert.True(result.IsSuccess);
        Assert.Equal(root.SectionId, result.Value.SectionId);
        Assert.Equal(root.Id, result.Value.ParentId);
        Assert.Equal(1, result.Value.Depth);
        Assert.True(result.Value.IsFolder);
        Assert.Equal("CLR", result.Value.Name!.Value);
        Assert.Equal(FolderColor.Teal, result.Value.Color);
        Assert.Equal(FolderIcon.DotNet, result.Value.Icon);
        Assert.Equal(LibraryContainer.DefaultDisplayOrder, result.Value.DisplayOrder);
    }

    [Fact]
    public void CreateFolder_AllowsThreeFolderLevels()
    {
        LibraryContainer root = CreateRoot();
        LibraryContainer level1 = CreateFolder(root, "Level 1");
        LibraryContainer level2 = CreateFolder(level1, "Level 2");

        var level3Result = LibraryContainer.CreateFolder(
            level2,
            FolderName.Create("Level 3").Value,
            FolderColor.Teal,
            FolderIcon.Folder);

        Assert.True(level3Result.IsSuccess);
        Assert.Equal(LibraryContainer.MaxFolderDepth, level3Result.Value.Depth);
    }

    [Fact]
    public void CreateFolder_RejectsFourthFolderLevel()
    {
        LibraryContainer root = CreateRoot();
        LibraryContainer level1 = CreateFolder(root, "Level 1");
        LibraryContainer level2 = CreateFolder(level1, "Level 2");
        LibraryContainer level3 = CreateFolder(level2, "Level 3");

        var result = LibraryContainer.CreateFolder(
            level3,
            FolderName.Create("Level 4").Value,
            FolderColor.Teal,
            FolderIcon.Folder);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "library.container.maximum.folder.depth.exceeded",
            result.Error.Code);
    }

    [Fact]
    public void UpdateFolder_ChangesFolderButCannotChangeRoot()
    {
        LibraryContainer root = CreateRoot();
        LibraryContainer folder = CreateFolder(root, "Old name");

        var folderResult = folder.UpdateFolder(
            FolderName.Create("New name").Value,
            FolderColor.Purple,
            FolderIcon.Database);
        var rootResult = root.UpdateFolder(
            FolderName.Create("Root name").Value,
            FolderColor.Purple,
            FolderIcon.Database);

        Assert.True(folderResult.IsSuccess);
        Assert.Equal("New name", folder.Name!.Value);
        Assert.Equal(FolderColor.Purple, folder.Color);
        Assert.Equal(FolderIcon.Database, folder.Icon);

        Assert.True(rootResult.IsFailure);
        Assert.Null(root.Name);
        Assert.Null(root.Color);
        Assert.Null(root.Icon);
    }

    [Fact]
    public void ChangeDisplayOrder_RejectsNegativeValue()
    {
        LibraryContainer folder = CreateFolder(CreateRoot(), "Folder");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => folder.ChangeDisplayOrder(-1));
    }

    private static LibraryContainer CreateRoot() =>
        LibraryContainer.CreateRoot(SectionId.New()).Value;

    private static LibraryContainer CreateFolder(
        LibraryContainer parent,
        string name) =>
        LibraryContainer.CreateFolder(
            parent,
            FolderName.Create(name).Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;
}
