namespace Mnemora.Contracts.Library;

public sealed record LibraryMaterialsPageDto(
    LibraryTopicHeaderDto Topic,
    IReadOnlyList<LibraryMaterialDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount = 0)
{
    /// <summary>
    /// Новая модель текущего расположения. Topic оставлен как compatibility-header,
    /// пока Desktop полностью не перейдёт на LibraryContainer.
    /// </summary>
    public LibraryContainerHeaderDto? Container { get; init; }
}
