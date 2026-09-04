using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public enum LibrarySectionManagementTreeNodeKind
{
    Section,
    Folder,
    Placeholder,
    LoadMore,
    Error,
}

public sealed partial class LibrarySectionManagementTreeNodeViewModel : ObservableObject
{
    private LibrarySectionManagementTreeNodeViewModel(
        LibrarySectionManagementTreeNodeKind kind,
        string name,
        Guid? containerId = null,
        Guid? sectionId = null,
        string? color = null,
        string? icon = null,
        int depth = 0,
        int childFoldersCount = 0,
        LibrarySectionManagementTreeNodeViewModel? parent = null)
    {
        Kind = kind;
        Name = name;
        ContainerId = containerId;
        SectionId = sectionId;
        Color = color;
        Icon = icon;
        Depth = Math.Max(0, depth);
        ChildFoldersCount = Math.Max(0, childFoldersCount);
        Parent = parent;
    }

    public LibrarySectionManagementTreeNodeKind Kind { get; }
    public string Name { get; }
    public Guid? ContainerId { get; }
    public Guid? SectionId { get; }
    public string? Color { get; }
    public string? Icon { get; }
    public int Depth { get; }
    public int ChildFoldersCount { get; }
    public LibrarySectionManagementTreeNodeViewModel? Parent { get; }
    public ObservableCollection<LibrarySectionManagementTreeNodeViewModel> Children { get; } = [];

    public bool IsSection => Kind == LibrarySectionManagementTreeNodeKind.Section;
    public bool IsFolder => Kind == LibrarySectionManagementTreeNodeKind.Folder;
    public bool IsPlaceholder => Kind == LibrarySectionManagementTreeNodeKind.Placeholder;
    public bool IsLoadMore => Kind == LibrarySectionManagementTreeNodeKind.LoadMore;
    public bool IsError => Kind == LibrarySectionManagementTreeNodeKind.Error;
    public bool IsNavigationNode => IsSection || IsFolder;
    public bool CanCreateChildFolder => IsNavigationNode && Depth < 3;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoading;

    internal bool ChildrenLoaded { get; set; }
    internal int NextOffset { get; set; }
    internal bool HasMore { get; set; }

    public static LibrarySectionManagementTreeNodeViewModel CreateSection(
        LibrarySectionOverviewDto section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var node = new LibrarySectionManagementTreeNodeViewModel(
            LibrarySectionManagementTreeNodeKind.Section,
            section.Name,
            section.RootContainerId,
            section.Id,
            section.Color,
            section.Icon,
            depth: 0,
            childFoldersCount: section.FoldersCount);

        node.AddPlaceholderIfNeeded();
        return node;
    }

    public static LibrarySectionManagementTreeNodeViewModel CreateFolder(
        LibraryHierarchyFolderDto folder,
        LibrarySectionManagementTreeNodeViewModel parent)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(parent);

        var node = new LibrarySectionManagementTreeNodeViewModel(
            LibrarySectionManagementTreeNodeKind.Folder,
            folder.Name,
            folder.Id,
            folder.SectionId,
            folder.Color,
            folder.Icon,
            parent.Depth + 1,
            folder.ChildFoldersCount,
            parent);

        node.AddPlaceholderIfNeeded();
        return node;
    }

    public static LibrarySectionManagementTreeNodeViewModel CreateLoadMore(
        LibrarySectionManagementTreeNodeViewModel parent) =>
        new(
            LibrarySectionManagementTreeNodeKind.LoadMore,
            "Показать ещё папки",
            parent: parent);

    public static LibrarySectionManagementTreeNodeViewModel CreateError(
        LibrarySectionManagementTreeNodeViewModel parent,
        string message) =>
        new(
            LibrarySectionManagementTreeNodeKind.Error,
            message,
            parent: parent);

    private void AddPlaceholderIfNeeded()
    {
        if (ChildFoldersCount <= 0)
        {
            ChildrenLoaded = true;
            return;
        }

        Children.Add(
            new LibrarySectionManagementTreeNodeViewModel(
                LibrarySectionManagementTreeNodeKind.Placeholder,
                "Загрузка...",
                parent: this));
    }
}
