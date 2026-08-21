using CSharpFunctionalExtensions;
using Mnemora.Domain.Sections;
using Mnemora.Shared;

namespace Mnemora.Domain.LibraryContainers;

/// <summary>
/// Внутренний контейнер библиотеки.
/// Корневой контейнер представляет сам раздел как место хранения,
/// а остальные контейнеры являются пользовательскими папками.
/// </summary>
public sealed class LibraryContainer
{
    public const int RootDepth = 0;
    public const int MaxFolderDepth = 3;
    public const int DefaultDisplayOrder = int.MaxValue;

    public LibraryContainerId Id { get; private set; } = null!;

    public SectionId SectionId { get; private set; } = null!;

    /// <summary>
    /// Родительский контейнер. У корневого контейнера раздела отсутствует.
    /// </summary>
    public LibraryContainerId? ParentId { get; private set; }

    /// <summary>
    /// Глубина контейнера: root = 0, пользовательские папки = 1..3.
    /// </summary>
    public int Depth { get; private set; }

    /// <summary>
    /// Название пользовательской папки. Для root равно null.
    /// </summary>
    public FolderName? Name { get; private set; }

    /// <summary>
    /// Цвет пользовательской папки. Для root равно null.
    /// </summary>
    public FolderColor? Color { get; private set; }

    /// <summary>
    /// Иконка пользовательской папки. Для root равно null.
    /// </summary>
    public FolderIcon? Icon { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Позиция папки среди соседних папок одного родительского контейнера.
    /// Для root не используется.
    /// </summary>
    public int DisplayOrder { get; private set; } = DefaultDisplayOrder;

    public bool IsRoot => ParentId is null;

    public bool IsFolder => ParentId is not null;

    // EF Core
    private LibraryContainer()
    {
    }

    private LibraryContainer(SectionId sectionId)
    {
        var now = DateTime.UtcNow;

        Id = LibraryContainerId.New();
        SectionId = sectionId;
        ParentId = null;
        Depth = RootDepth;
        Name = null;
        Color = null;
        Icon = null;
        DisplayOrder = DefaultDisplayOrder;
        CreatedAt = now;
        UpdatedAt = now;
    }

    private LibraryContainer(
        LibraryContainer parent,
        FolderName name,
        FolderColor color,
        FolderIcon icon)
    {
        var now = DateTime.UtcNow;

        Id = LibraryContainerId.New();
        SectionId = parent.SectionId;
        ParentId = parent.Id;
        Depth = parent.Depth + 1;
        Name = name;
        Color = color;
        Icon = icon;
        DisplayOrder = DefaultDisplayOrder;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Result<LibraryContainer, Error> CreateRoot(SectionId? sectionId)
    {
        if (sectionId is null)
        {
            return CommonErrors.IsRequired(nameof(sectionId));
        }

        return new LibraryContainer(sectionId);
    }

    public static Result<LibraryContainer, Error> CreateFolder(
        LibraryContainer? parent,
        FolderName? name,
        FolderColor color,
        FolderIcon icon)
    {
        if (parent is null)
        {
            return CommonErrors.IsRequired(nameof(parent));
        }

        if (name is null)
        {
            return CommonErrors.IsRequired(nameof(name));
        }

        if (!Enum.IsDefined(color))
        {
            return LibraryContainerErrors.FolderColorIsInvalid(nameof(color));
        }

        if (!Enum.IsDefined(icon))
        {
            return LibraryContainerErrors.FolderIconIsInvalid(nameof(icon));
        }

        if (parent.Depth >= MaxFolderDepth)
        {
            return LibraryContainerErrors.MaximumFolderDepthExceeded(MaxFolderDepth);
        }

        return new LibraryContainer(
            parent,
            name,
            color,
            icon);
    }

    public UnitResult<Error> UpdateFolder(
        FolderName? name,
        FolderColor color,
        FolderIcon icon)
    {
        if (IsRoot)
        {
            return LibraryContainerErrors.RootContainerCannotBeUpdatedAsFolder();
        }

        if (name is null)
        {
            return CommonErrors.IsRequired(nameof(name));
        }

        if (!Enum.IsDefined(color))
        {
            return LibraryContainerErrors.FolderColorIsInvalid(nameof(color));
        }

        if (!Enum.IsDefined(icon))
        {
            return LibraryContainerErrors.FolderIconIsInvalid(nameof(icon));
        }

        if (Name == name &&
            Color == color &&
            Icon == icon)
        {
            return UnitResult.Success<Error>();
        }

        Name = name;
        Color = color;
        Icon = icon;
        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    public void ChangeDisplayOrder(int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        DisplayOrder = displayOrder;
    }
}
