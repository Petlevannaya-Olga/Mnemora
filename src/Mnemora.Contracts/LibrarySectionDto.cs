namespace Mnemora.Contracts;

public sealed record LibrarySectionDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    IReadOnlyList<LibraryTopicDto> Topics);