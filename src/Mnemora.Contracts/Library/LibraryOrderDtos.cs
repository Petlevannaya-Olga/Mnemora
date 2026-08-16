namespace Mnemora.Contracts.Library;

public sealed record LibraryOrderItemDto(
    Guid Id,
    string Name,
    string Icon,
    string? Color,
    string Details,
    int DisplayOrder);

public sealed record LibraryOrderTopicScopeDto(Guid Id, string Name);

public sealed record LibraryOrderSectionScopeDto(
    Guid Id,
    string Name,
    IReadOnlyList<LibraryOrderTopicScopeDto> Topics);

public sealed record LibraryOrderScopesDto(IReadOnlyList<LibraryOrderSectionScopeDto> Sections);
