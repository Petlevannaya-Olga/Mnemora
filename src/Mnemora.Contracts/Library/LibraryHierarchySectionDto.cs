namespace Mnemora.Contracts.Library;

public sealed record LibraryHierarchySectionDto(
    Guid Id,
    Guid RootContainerId,
    string Name,
    string Color,
    string Icon,
    int ChildFoldersCount);
