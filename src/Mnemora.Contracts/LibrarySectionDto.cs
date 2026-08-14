namespace Mnemora.Contracts;

public sealed record LibrarySectionDto(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    IReadOnlyList<LibraryTopicDto> Topics);