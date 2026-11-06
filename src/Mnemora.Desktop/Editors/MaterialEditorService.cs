using System.IO;
using Mnemora.Application.Storage;
using Mnemora.Desktop.Notifications;

namespace Mnemora.Desktop.Editors;

public sealed class MaterialEditorService(
    IStoragePathProvider storagePathProvider,
    IMarkdownEditorService markdownEditorService,
    INotificationService notificationService)
    : IMaterialEditorService
{
    private const string MaterialsDirectoryName = "materials";
    private const string ArticlesDirectoryName = "articles";
    private const string ArticleFileName = "content.md";

    public async Task OpenArticleInEditorAsync(
        Guid articleId,
        CancellationToken cancellationToken = default)
    {
        if (articleId == Guid.Empty)
        {
            notificationService.ShowError("Не удалось определить статью для открытия.");
            return;
        }

        var storagePathResult = storagePathProvider.GetStoragePath();

        if (storagePathResult.IsFailure)
        {
            notificationService.ShowError(storagePathResult.Error.Message);
            return;
        }

        string articlePath = Path.Combine(
            storagePathResult.Value,
            MaterialsDirectoryName,
            ArticlesDirectoryName,
            articleId.ToString("N"),
            ArticleFileName);

        try
        {
            MarkdownEditorLaunchResult result =
                await markdownEditorService.OpenAsync(
                    articlePath,
                    cancellationToken);

            if (!result.IsSuccess)
            {
                notificationService.ShowError(result.Message);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Навигация или закрытие экрана отменили открытие.
        }
        catch (Exception)
        {
            notificationService.ShowError(
                "Не удалось открыть статью в настроенном Markdown-редакторе.");
        }
    }
}
