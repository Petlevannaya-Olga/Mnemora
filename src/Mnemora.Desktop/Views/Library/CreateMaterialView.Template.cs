using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView
{
    private readonly string _templateSessionId = Guid.NewGuid().ToString("N");

    private string? _articleTemplatePath;
    private string? _questionTemplatePath;
    private string? _answerTemplatePath;

    private void CreateMarkdownTemplate_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        e.Handled = true;

        try
        {
            string? selectedPath = GetSelectedPath(source);
            string? templatePath = GetOwnedTemplatePath(source);

            if (!string.IsNullOrWhiteSpace(templatePath) &&
                File.Exists(templatePath))
            {
                SetSelectedFile(source, templatePath);
                OpenTemplateFile(source, templatePath);
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                MessageBoxResult replaceResult = MessageBox.Show(
                    "Для материала уже выбран Markdown-файл. Заменить его новым файлом по шаблону?",
                    "Mnemora",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (replaceResult != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            string path = CreateTemplateFile(source);
            SetOwnedTemplatePath(source, path);

            // Используем тот же проверенный путь смены состояния,
            // что и выбор файла / drag & drop.
            SetSelectedFile(source, path);
            OpenTemplateFile(source, path);
        }
        catch (Exception)
        {
            ShowFileError(source, "Не удалось создать Markdown-файл по шаблону.");
        }
    }

    private string CreateTemplateFile(string source)
    {
        string directory = GetTemplateDirectory();
        Directory.CreateDirectory(directory);

        string fileName = source switch
        {
            ArticleSource => "article.md",
            QuestionSource => "question.md",
            AnswerSource => "answer.md",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

        string path = Path.Combine(directory, fileName);

        // В рамках одного мастера для каждого источника существует
        // ровно один физический файл-шаблон.
        if (!File.Exists(path))
        {
            File.WriteAllText(
                path,
                GetTemplateContent(source),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return Path.GetFullPath(path);
    }

    private void OpenTemplateFile(string source, string path)
    {
        if (!File.Exists(path))
        {
            ShowFileError(source, "Файл шаблона больше не найден.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            ShowFileError(source, "Файл создан, но не удалось открыть его в системном редакторе.");
        }
    }

    private void DeleteOwnedTemplateIfReplaced(string source, string newPath)
    {
        string? templatePath = GetOwnedTemplatePath(source);

        if (string.IsNullOrWhiteSpace(templatePath) ||
            PathsEqual(templatePath, newPath))
        {
            return;
        }

        DeleteOwnedTemplate(source);
    }

    private void DeleteOwnedTemplate(string source)
    {
        string? templatePath = GetOwnedTemplatePath(source);

        if (string.IsNullOrWhiteSpace(templatePath))
        {
            return;
        }

        try
        {
            if (File.Exists(templatePath))
            {
                File.Delete(templatePath);
            }
        }
        catch (IOException)
        {
            // Сброс выбранного файла не должен ломаться из-за невозможности
            // удалить временный черновик (например, если редактор держит файл).
        }
        catch (UnauthorizedAccessException)
        {
            // ignore
        }
        finally
        {
            SetOwnedTemplatePath(source, null);
            TryDeleteTemplateDirectoryIfEmpty();
        }
    }

    private string? GetOwnedTemplatePath(string source) =>
        source switch
        {
            ArticleSource => _articleTemplatePath,
            QuestionSource => _questionTemplatePath,
            AnswerSource => _answerTemplatePath,
            _ => null,
        };

    private void SetOwnedTemplatePath(string source, string? path)
    {
        switch (source)
        {
            case ArticleSource:
                _articleTemplatePath = path;
                break;
            case QuestionSource:
                _questionTemplatePath = path;
                break;
            case AnswerSource:
                _answerTemplatePath = path;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, null);
        }
    }

    private string GetTemplateDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Mnemora",
            "Temp",
            _templateSessionId);

    private void TryDeleteTemplateDirectoryIfEmpty()
    {
        string directory = GetTemplateDirectory();

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
