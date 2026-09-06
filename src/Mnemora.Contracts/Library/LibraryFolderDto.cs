namespace Mnemora.Contracts.Library;

/// <summary>
/// Пользовательская папка библиотеки в постраничном списке.
/// ChildFoldersCount и MaterialsCount относятся только к непосредственному
/// содержимому этой папки и не агрегируют всё поддерево.
/// </summary>
public sealed record LibraryFolderDto(
    Guid Id,
    Guid SectionId,
    Guid ParentId,
    int Depth,
    string Name,
    string Color,
    string Icon,
    int DisplayOrder,
    int ChildFoldersCount,
    int MaterialsCount,
    bool CanCreateChildFolder,
    DateTime CreatedAt,
    DateTime UpdatedAt);
