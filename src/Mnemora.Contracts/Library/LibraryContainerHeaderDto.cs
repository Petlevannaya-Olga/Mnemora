namespace Mnemora.Contracts.Library;

/// <summary>
/// Заголовок текущего места в библиотеке.
/// Для root отображаемые имя/цвет/иконка берутся у раздела.
/// </summary>
public sealed record LibraryContainerHeaderDto(
    Guid Id,
    Guid SectionId,
    string SectionName,
    Guid? ParentId,
    int Depth,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public bool IsRoot => ParentId is null;

    public bool IsFolder => ParentId is not null;
}
