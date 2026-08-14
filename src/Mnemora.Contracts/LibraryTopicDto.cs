namespace Mnemora.Contracts;

public sealed record LibraryTopicDto(
    Guid Id,
    string Name,
    DateTime CreatedAt);