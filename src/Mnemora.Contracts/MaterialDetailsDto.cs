namespace Mnemora.Contracts;

public sealed record MaterialMetadataDto(
    Guid Id,
    Guid TopicId,
    string TopicName,
    Guid SectionId,
    string SectionName,
    string Title,
    string Type,
    string Difficulty,
    string Icon,
    int StudyPoints,
    int ReviewPoints,
    int LearningRevision,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public abstract record MaterialDetailsDto(MaterialMetadataDto Metadata);

public sealed record ArticleDetailsDto(
    MaterialMetadataDto Metadata,
    string BodyMarkdown)
    : MaterialDetailsDto(Metadata);

public sealed record RelatedArticleDto(Guid Id, string Title);

public sealed record QuestionDetailsDto(
    MaterialMetadataDto Metadata,
    RelatedArticleDto? Article,
    string PromptMarkdown,
    string ReferenceAnswerMarkdown)
    : MaterialDetailsDto(Metadata);