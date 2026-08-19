namespace Mnemora.Contracts.Library;

public sealed record LibraryTopicDto(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public IReadOnlyList<LibraryMaterialDto> Materials { get; init; } = [];
}