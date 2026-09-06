using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public enum LibraryHierarchyNodeKind
{
    Library,
    Section,
    Folder,
    Placeholder,
    LoadMore,
    Error,
}

public sealed partial class LibraryHierarchyNodeViewModel : ObservableObject
{
    private LibraryHierarchyNodeViewModel(
        LibraryHierarchyNodeKind kind,
        string name,
        LibraryHierarchyNodeViewModel? parent = null,
        Guid? containerId = null,
        Guid? sectionId = null,
        string? color = null,
        string? icon = null,
        int childFoldersCount = 0)
    {
        Kind = kind;
        Name = name;
        Parent = parent;
        ContainerId = containerId;
        SectionId = sectionId;
        Color = color;
        Icon = icon;
        ChildFoldersCount = Math.Max(0, childFoldersCount);
    }

    public LibraryHierarchyNodeKind Kind { get; }
    public string Name { get; }
    public LibraryHierarchyNodeViewModel? Parent { get; }
    public Guid? ContainerId { get; }
    public Guid? SectionId { get; }
    public string? Color { get; }
    public string? Icon { get; }
    public int ChildFoldersCount { get; }
    public ObservableCollection<LibraryHierarchyNodeViewModel> Children { get; } = [];

    public bool IsLibrary => Kind == LibraryHierarchyNodeKind.Library;
    public bool IsSection => Kind == LibraryHierarchyNodeKind.Section;
    public bool IsFolder => Kind == LibraryHierarchyNodeKind.Folder;
    public bool IsPlaceholder => Kind == LibraryHierarchyNodeKind.Placeholder;
    public bool IsLoadMore => Kind == LibraryHierarchyNodeKind.LoadMore;
    public bool IsError => Kind == LibraryHierarchyNodeKind.Error;
    public bool IsNavigationNode => IsLibrary || IsSection || IsFolder;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isLoading;

    internal bool ChildrenLoaded { get; set; }
    internal int NextOffset { get; set; }
    internal bool HasMore { get; set; }

    public static LibraryHierarchyNodeViewModel CreateLibrary() =>
        new(
            LibraryHierarchyNodeKind.Library,
            "Библиотека",
            icon: "HOME");

    public static LibraryHierarchyNodeViewModel CreateSection(
        LibraryHierarchySectionDto source,
        LibraryHierarchyNodeViewModel parent)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parent);

        var node = new LibraryHierarchyNodeViewModel(
            LibraryHierarchyNodeKind.Section,
            source.Name,
            parent,
            source.RootContainerId,
            source.Id,
            source.Color,
            source.Icon,
            source.ChildFoldersCount);

        node.AddPlaceholderIfNeeded();
        return node;
    }

    public static LibraryHierarchyNodeViewModel CreateSection(
        LibraryContainerContentsDto root,
        LibraryHierarchyNodeViewModel parent)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(parent);

        var node = new LibraryHierarchyNodeViewModel(
            LibraryHierarchyNodeKind.Section,
            root.Section.Name,
            parent,
            root.Container.Id,
            root.Section.Id,
            root.Section.Color,
            root.Section.Icon,
            root.FoldersCount);

        node.AddPlaceholderIfNeeded();
        return node;
    }

    public static LibraryHierarchyNodeViewModel CreateFolder(
        LibraryHierarchyFolderDto source,
        LibraryHierarchyNodeViewModel parent)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parent);

        var node = new LibraryHierarchyNodeViewModel(
            LibraryHierarchyNodeKind.Folder,
            source.Name,
            parent,
            source.Id,
            source.SectionId,
            source.Color,
            source.Icon,
            source.ChildFoldersCount);

        node.AddPlaceholderIfNeeded();
        return node;
    }

    public static LibraryHierarchyNodeViewModel CreateFolder(
        LibraryContainerContentsDto contents,
        LibraryHierarchyNodeViewModel parent)
    {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(parent);

        var node = new LibraryHierarchyNodeViewModel(
            LibraryHierarchyNodeKind.Folder,
            contents.Container.Name,
            parent,
            contents.Container.Id,
            contents.Container.SectionId,
            contents.Container.Color,
            contents.Container.Icon,
            contents.FoldersCount);

        node.AddPlaceholderIfNeeded();
        return node;
    }

    public static LibraryHierarchyNodeViewModel CreateLoadMore(
        LibraryHierarchyNodeViewModel parent,
        string name) =>
        new(
            LibraryHierarchyNodeKind.LoadMore,
            name,
            parent);

    public static LibraryHierarchyNodeViewModel CreateError(
        LibraryHierarchyNodeViewModel parent,
        string name) =>
        new(
            LibraryHierarchyNodeKind.Error,
            name,
            parent);

    private void AddPlaceholderIfNeeded()
    {
        if (ChildFoldersCount <= 0)
        {
            ChildrenLoaded = true;
            return;
        }

        Children.Add(
            new LibraryHierarchyNodeViewModel(
                LibraryHierarchyNodeKind.Placeholder,
                "Загрузка...",
                this));
    }
}
