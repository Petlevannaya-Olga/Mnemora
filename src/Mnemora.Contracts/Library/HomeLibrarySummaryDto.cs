namespace Mnemora.Contracts.Library;

public sealed record HomeLibrarySummaryDto(
    int SectionsCount,
    int TopicsCount,
    HomeSuggestedSectionDto? SuggestedSection);

public sealed record HomeSuggestedSectionDto(
    Guid Id,
    string Name);
