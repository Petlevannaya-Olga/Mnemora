using Mnemora.Desktop.Settings;

namespace Mnemora.Desktop.Editors;

public interface IMarkdownEditorService
{
    string? FindVisualStudioCodeExecutable();

    bool IsObsidianInstalled();

    MarkdownEditorLaunchResult OpenDownloadPage(
        MarkdownEditorType editor);

    Task<MarkdownEditorLaunchResult> CheckAsync(
        MarkdownEditorType editor,
        string? visualStudioCodePath,
        string? obsidianVaultPath,
        CancellationToken cancellationToken = default);

    Task<MarkdownEditorLaunchResult> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
