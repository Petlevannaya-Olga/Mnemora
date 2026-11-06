namespace Mnemora.Contracts;

public sealed record StandaloneQuestionPickerOptionDto(
    Guid Id,
    string Title,
    string Difficulty,
    int StudyPoints,
    int ReviewPoints,
    Guid ContainerId,
    string ContainerName,
    Guid SectionId,
    string SectionName);
