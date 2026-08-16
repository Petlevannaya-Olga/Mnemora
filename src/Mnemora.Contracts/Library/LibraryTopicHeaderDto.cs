namespace Mnemora.Contracts.Library;

public sealed record LibraryTopicHeaderDto(
    Guid Id,
    Guid SectionId,
    string SectionName,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt);