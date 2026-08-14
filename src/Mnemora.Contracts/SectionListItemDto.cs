namespace Mnemora.Contracts;

public sealed record SectionListItemDto(
    Guid Id,
    string Name,
    DateTime CreatedAt);