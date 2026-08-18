namespace Mnemora.Contracts;

public sealed record MaterialLearningLinkOptionDto(
    Guid Id,
    string Title,
    string Difficulty,
    int StudyPoints,
    int ReviewPoints);

public sealed record MaterialLearningLinkOptionsDto(
    IReadOnlyList<MaterialLearningLinkOptionDto> StandaloneQuestions,
    IReadOnlyList<MaterialLearningLinkOptionDto> Articles);
