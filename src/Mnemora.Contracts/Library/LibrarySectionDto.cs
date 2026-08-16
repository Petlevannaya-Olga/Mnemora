namespace Mnemora.Contracts.Library;

public sealed record LibrarySectionDto(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastActivityAt,
    IReadOnlyList<LibraryTopicDto> Topics);