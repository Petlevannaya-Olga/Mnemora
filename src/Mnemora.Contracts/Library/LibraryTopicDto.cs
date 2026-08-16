namespace Mnemora.Contracts.Library;

public sealed record LibraryTopicDto(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt)
{
    public IReadOnlyList<LibraryMaterialDto> Materials { get; init; } = [];
}