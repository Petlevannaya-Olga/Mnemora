namespace Mnemora.Desktop.Editors;

public sealed record MarkdownEditorConfigurationValidationResult(
    bool IsValid,
    string Message);
