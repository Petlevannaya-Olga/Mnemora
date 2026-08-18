namespace Mnemora.Contracts;

public sealed record StandaloneQuestionPickerOptionDto(
    Guid Id,
    string Title,
    string Difficulty,
    int StudyPoints,
    int ReviewPoints,
    Guid TopicId,
    string TopicName,
    Guid SectionId,
    string SectionName);
