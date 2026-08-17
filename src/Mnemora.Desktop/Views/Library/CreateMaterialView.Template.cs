using System.IO;
using System.Text;
using System.Windows;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView
{
    private void CreateMarkdownTemplate_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        try
        {
            string path = CreateTemplateFile(source);

            // Используем тот же рабочий путь, что и выбор файла / drag & drop.
            SetSelectedFile(source, path);
        }
        catch (Exception)
        {
            ShowFileError(source, "Не удалось создать Markdown-файл по шаблону.");
        }

        e.Handled = true;
    }

    private static string CreateTemplateFile(string source)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "Mnemora",
            "Drafts");

        Directory.CreateDirectory(directory);

        string baseName = source switch
        {
            ArticleSource => "material",
            QuestionSource => "question",
            AnswerSource => "answer",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

        string path = GetAvailableTemplatePath(directory, baseName);
        string content = GetTemplateContent(source);

        File.WriteAllText(
            path,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return path;
    }

    private static string GetAvailableTemplatePath(
        string directory,
        string baseName)
    {
        string path = Path.Combine(directory, $"{baseName}.md");

        if (!File.Exists(path))
        {
            return path;
        }

        for (int number = 2; ; number++)
        {
            path = Path.Combine(directory, $"{baseName}-{number}.md");

            if (!File.Exists(path))
            {
                return path;
            }
        }
    }

    private static string GetTemplateContent(string source) =>
        source switch
        {
            ArticleSource =>
                "## Основное\n\n" +
                "\n\n## Пример\n\n" +
                "```csharp\n\n```\n",

            QuestionSource =>
                "## Вопрос\n\n",

            AnswerSource =>
                "## Эталонный ответ\n\n",

            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };
}
