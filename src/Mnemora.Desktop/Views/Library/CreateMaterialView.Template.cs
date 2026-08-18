using System.IO;
using System.Text;
using System.Windows;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Library;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView
{
    private readonly string _templateSessionId = Guid.NewGuid().ToString("N");

    private readonly HashSet<string> _ownedDraftPaths =
        new(StringComparer.OrdinalIgnoreCase);

    private string? _templateDirectory;

    private async void CreateMarkdownTemplate_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        e.Handled = true;

        try
        {
            string? selectedPath =
                GetSelectedPath(source);

            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                string selectedFileName =
                    Path.GetFileName(selectedPath);

                if (!ShowReplaceDraftDialog(
                        selectedFileName))
                {
                    return;
                }
            }

            string path =
                await CreateTemplateFileAsync(source);

            RegisterOwnedDraft(path);

            // Создание файла только меняет текущий файл мастера.
            // Внешний редактор открывается исключительно по явной кнопке
            // «Открыть в редакторе».
            SetSelectedFile(source, path);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception)
        {
            ShowFileError(
                source,
                "Не удалось создать Markdown-файл по шаблону.");
        }
    }

    private bool ShowReplaceDraftDialog(
        string selectedFileName)
    {
        var dialog =
            new ReplaceMaterialDraftDialogWindow(
                selectedFileName);

        Window? owner =
            Window.GetWindow(this);

        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        var overlayHost =
            System.Windows.Application.Current.MainWindow
                as IDialogOverlayHost;

        overlayHost?.ShowDialogOverlay();

        try
        {
            return dialog.ShowDialog() == true;
        }
        finally
        {
            overlayHost?.HideDialogOverlay();
        }
    }

    private async Task<string> CreateTemplateFileAsync(
        string source)
    {
        string directory =
            await GetTemplateDirectoryAsync();

        Directory.CreateDirectory(directory);

        string fileName = source switch
        {
            ArticleSource => "article.md",
            QuestionSource => "question.md",
            AnswerSource => "answer.md",
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                null),
        };

        string path =
            GetUniqueFilePath(
                directory,
                fileName);

        File.WriteAllText(
            path,
            GetTemplateContent(source),
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false));

        return Path.GetFullPath(path);
    }

    private async Task<string> ImportMarkdownIntoDraftAsync(
        string source,
        string sourcePath)
    {
        string fullSourcePath =
            Path.GetFullPath(sourcePath);

        string draftDirectory =
            await GetTemplateDirectoryAsync();

        string sourceDirectoryName = source switch
        {
            ArticleSource => "article",
            QuestionSource => "question",
            AnswerSource => "answer",
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                null),
        };

        string importDirectory =
            Path.Combine(
                draftDirectory,
                "imports",
                sourceDirectoryName);

        Directory.CreateDirectory(importDirectory);

        string destinationPath =
            GetUniqueFilePath(
                importDirectory,
                Path.GetFileName(fullSourcePath));

        if (PathsEqual(
                fullSourcePath,
                destinationPath))
        {
            return fullSourcePath;
        }

        // Копируем, а не перемещаем: исходный пользовательский файл
        // остаётся на месте. Дальше мастер работает только со своей копией.
        File.Copy(
            fullSourcePath,
            destinationPath,
            overwrite: true);

        return destinationPath;
    }

    private void RegisterOwnedDraft(
        string path)
    {
        _ownedDraftPaths.Add(
            Path.GetFullPath(path));
    }

    private void DeleteAllOwnedDrafts()
    {
        foreach (string ownedDraftPath in
                 _ownedDraftPaths.ToArray())
        {
            try
            {
                if (File.Exists(ownedDraftPath))
                {
                    File.Delete(ownedDraftPath);
                }
            }
            catch (IOException)
            {
                // Редактор может ещё удерживать файл.
                // Очистка временных файлов выполняется best effort.
            }
            catch (UnauthorizedAccessException)
            {
                // ignore
            }
        }

        _ownedDraftPaths.Clear();

        TryDeleteTemplateDirectoryTree();
    }

    private async Task<string> GetTemplateDirectoryAsync()
    {
        if (!string.IsNullOrWhiteSpace(_templateDirectory))
        {
            return _templateDirectory;
        }

        if (DataContext is not CreateMaterialViewModel viewModel)
        {
            throw new InvalidOperationException(
                "Не удалось определить состояние мастера создания материала.");
        }

        _templateDirectory =
            await viewModel.GetDraftDirectoryAsync(
                _templateSessionId);

        return _templateDirectory;
    }

    private void TryDeleteTemplateDirectoryTree()
    {
        if (string.IsNullOrWhiteSpace(_templateDirectory))
        {
            return;
        }

        string sessionDirectory =
            _templateDirectory;

        string? createMaterialDirectory =
            Path.GetDirectoryName(sessionDirectory);

        string? draftsRootDirectory =
            createMaterialDirectory is null
                ? null
                : Path.GetDirectoryName(createMaterialDirectory);

        try
        {
            if (Directory.Exists(sessionDirectory))
            {
                Directory.Delete(
                    sessionDirectory,
                    recursive: true);
            }
        }
        catch (IOException)
        {
            // ignore
        }
        catch (UnauthorizedAccessException)
        {
            // ignore
        }

        if (createMaterialDirectory is not null)
        {
            TryDeleteDirectoryIfEmpty(
                createMaterialDirectory);
        }

        if (draftsRootDirectory is not null)
        {
            TryDeleteDirectoryIfEmpty(
                draftsRootDirectory);
        }

        if (!Directory.Exists(sessionDirectory))
        {
            _templateDirectory = null;
        }
    }

    private static void TryDeleteDirectoryIfEmpty(
        string directory)
    {
        try
        {
            if (Directory.Exists(directory) &&
                Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }
        catch (IOException)
        {
            // ignore
        }
        catch (UnauthorizedAccessException)
        {
            // ignore
        }
    }

    private static string GetUniqueFilePath(
        string directory,
        string fileName)
    {
        string baseName =
            Path.GetFileNameWithoutExtension(fileName);

        string extension =
            Path.GetExtension(fileName);

        string candidate =
            Path.Combine(
                directory,
                fileName);

        int suffix = 2;

        while (File.Exists(candidate))
        {
            candidate =
                Path.Combine(
                    directory,
                    $"{baseName}-{suffix}{extension}");

            suffix++;
        }

        return Path.GetFullPath(candidate);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static string GetTemplateContent(string source) =>
        source switch
        {
            ArticleSource =>
                "# Название материала\n\n" +
                "## Основное\n\n" +
                "\n\n## Пример\n\n" +
                "```csharp\n\n```\n",

            QuestionSource =>
                "# Вопрос\n\n",

            AnswerSource =>
                "# Эталонный ответ\n\n",

            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };
}
