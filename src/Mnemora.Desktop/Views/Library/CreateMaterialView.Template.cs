using System.IO;
using System.Text;
using System.Windows;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView
{
    private readonly string _templateDirectory = Path.Combine(
        Path.GetTempPath(),
        "Mnemora",
        "CreateMaterial",
        Guid.NewGuid().ToString("N"));

    private void CreateMarkdownTemplate_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_templateDirectory);

            string path = GetAvailableTemplatePath(source);
            File.WriteAllText(
                path,
                GetTemplateContent(source),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            SetSelectedFile(source, path);
        }
        catch (Exception exception)
            when (exception is IOException
                  or UnauthorizedAccessException
                  or ArgumentException
                  or NotSupportedException)
        {
            ShowFileError(source, "Не удалось создать Markdown-файл.");
        }

        e.Handled = true;
    }

    private string GetAvailableTemplatePath(string source)
    {
        string fileName = source switch
        {
            ArticleSource => "material.md",
            QuestionSource => "question.md",
            AnswerSource => "answer.md",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

        string path = Path.Combine(_templateDirectory, fileName);

        if (!File.Exists(path))
        {
            return path;
        }

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        for (int index = 2; ; index++)
        {
            path = Path.Combine(_templateDirectory, $"{name}-{index}{extension}");

            if (!File.Exists(path))
            {
                return path;
            }
        }
    }

    private static string GetTemplateContent(string source) =>
        source switch
        {
            ArticleSource => "# Материал\n\n",
            QuestionSource => "# Вопрос\n\n",
            AnswerSource => "# Эталонный ответ\n\n",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };
}
