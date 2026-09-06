namespace Mnemora.Contracts.Library;

/// <summary>
/// Метаданные одного контейнера библиотеки.
/// Списки папок и материалов загружаются отдельными paged-query,
/// поэтому DTO остаётся bounded независимо от количества дочерних элементов.
/// </summary>
public sealed record LibraryContainerContentsDto(
    LibraryContainerHeaderDto Container,
    LibrarySectionHeaderDto Section,
    int FoldersCount,
    int MaterialsCount,
    bool CanCreateChildFolder);
