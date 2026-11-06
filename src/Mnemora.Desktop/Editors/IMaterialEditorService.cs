namespace Mnemora.Desktop.Editors;

public interface IMaterialEditorService
{
    Task OpenArticleInEditorAsync(
        Guid articleId,
        CancellationToken cancellationToken = default);
}
