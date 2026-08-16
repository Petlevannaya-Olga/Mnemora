namespace Mnemora.Contracts;

public sealed record LibrarySectionHeaderDto(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt);