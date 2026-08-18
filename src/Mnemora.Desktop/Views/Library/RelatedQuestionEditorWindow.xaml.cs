using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Domain.Materials;

namespace Mnemora.Desktop.Views.Library;

public partial class RelatedQuestionEditorWindow : Window
{
    private const string PromptSource = "Prompt";
    private const string AnswerSource = "Answer";

    public RelatedQuestionEditorWindow()
    {
        InitializeComponent();
    }

    private void Cancel_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is CreateMaterialViewModel viewModel)
        {
            viewModel.CancelRelatedQuestionEditor();
        }

        DialogResult = false;
    }

    private async void MarkdownDropZone_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetMarkdownSource(element, out string source))
        {
            return;
        }

        if (e.OriginalSource is DependencyObject originalSource &&
            FindVisualParent<Button>(originalSource) is not null)
        {
            return;
        }

        e.Handled = true;
        await ChooseMarkdownFileAsync(source);
    }

    private void MarkdownDropZone_OnDragOver(
        object sender,
        DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = TryGetSingleMarkdownFile(e.Data, out _)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void MarkdownDropZone_OnDrop(
        object sender,
        DragEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement element ||
            !TryGetMarkdownSource(element, out string source) ||
            !TryGetSingleMarkdownFile(e.Data, out string path))
        {
            return;
        }

        await ImportMarkdownAsync(source, path);
    }

    private async Task ChooseMarkdownFileAsync(string source)
    {
        var dialog = new OpenFileDialog
        {
            Title = source == AnswerSource
                ? "Выбрать Markdown-файл эталонного ответа"
                : "Выбрать Markdown-файл вопроса",
            Filter = "Markdown (*.md)|*.md",
            Multiselect = false,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await ImportMarkdownAsync(source, dialog.FileName);
    }

    private async Task ImportMarkdownAsync(
        string source,
        string sourcePath)
    {
        if (DataContext is not CreateMaterialViewModel viewModel)
        {
            return;
        }

        string? destinationPath = source == AnswerSource
            ? viewModel.RelatedQuestionAnswerPath
            : viewModel.RelatedQuestionPromptPath;

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось определить временный файл вопроса.");
            return;
        }

        try
        {
            string fullSourcePath = Path.GetFullPath(sourcePath);
            string fullDestinationPath = Path.GetFullPath(destinationPath);

            if (!string.Equals(
                    Path.GetExtension(fullSourcePath),
                    ".md",
                    StringComparison.OrdinalIgnoreCase))
            {
                viewModel.SetRelatedQuestionEditorError(
                    "Можно выбрать только Markdown-файл с расширением .md.");
                return;
            }

            if (!File.Exists(fullSourcePath))
            {
                viewModel.SetRelatedQuestionEditorError(
                    "Выбранный Markdown-файл не найден.");
                return;
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(fullDestinationPath)!);

            if (!string.Equals(
                    fullSourcePath,
                    fullDestinationPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                await CopyFileAllowSharedReadAsync(
                    fullSourcePath,
                    fullDestinationPath);
            }

            viewModel.SetRelatedQuestionFileConfigured(
                source == AnswerSource,
                true);
            viewModel.SetRelatedQuestionEditorError(null);
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException
                      or PathTooLongException)
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось импортировать Markdown-файл. Проверь доступ к файлу и попробуй снова.");
        }
    }

    private async void CreateMarkdownTemplate_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel ||
            sender is not FrameworkElement element ||
            !TryGetMarkdownSource(element, out string source))
        {
            return;
        }

        string? path = source == AnswerSource
            ? viewModel.RelatedQuestionAnswerPath
            : viewModel.RelatedQuestionPromptPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось определить временный Markdown-файл.");
            return;
        }

        try
        {
            await File.WriteAllTextAsync(
                path,
                GetTemplateContent(source),
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            viewModel.SetRelatedQuestionFileConfigured(
                source == AnswerSource,
                true);
            viewModel.SetRelatedQuestionEditorError(null);
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException
                      or PathTooLongException)
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось создать Markdown-файл по шаблону.");
        }
    }

    private async void ClearMarkdown_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel ||
            sender is not FrameworkElement element ||
            !TryGetMarkdownSource(element, out string source))
        {
            return;
        }

        string? path = source == AnswerSource
            ? viewModel.RelatedQuestionAnswerPath
            : viewModel.RelatedQuestionPromptPath;

        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                await File.WriteAllTextAsync(
                    path,
                    string.Empty,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false));
            }

            viewModel.SetRelatedQuestionFileConfigured(
                source == AnswerSource,
                false);
            viewModel.SetRelatedQuestionEditorError(null);
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException)
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось сбросить Markdown-файл.");
        }
    }

    private async void OpenMarkdown_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel ||
            sender is not FrameworkElement element)
        {
            return;
        }

        string? path =
            string.Equals(
                element.Tag as string,
                AnswerSource,
                StringComparison.Ordinal)
                ? viewModel.RelatedQuestionAnswerPath
                : viewModel.RelatedQuestionPromptPath;

        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path))
        {
            viewModel.SetRelatedQuestionEditorError(
                "Markdown-файл больше не найден. Закрой окно и создай вопрос заново.");
            return;
        }

        try
        {
            MarkdownEditorLaunchResult result =
                await viewModel.OpenMarkdownAsync(path);

            if (!result.IsSuccess)
            {
                viewModel.SetRelatedQuestionEditorError(result.Message);
                return;
            }

            viewModel.SetRelatedQuestionEditorError(null);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception)
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось открыть файл в настроенном Markdown-редакторе.");
        }
    }

    private async void Save_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel)
        {
            return;
        }

        viewModel.SetRelatedQuestionEditorError(null);
        viewModel.MarkRelatedQuestionValidationAttempted();

        if (viewModel.HasRelatedQuestionValidationError)
        {
            return;
        }

        if (!viewModel.IsRelatedQuestionPromptConfigured ||
            !viewModel.IsRelatedQuestionAnswerConfigured)
        {
            viewModel.SetRelatedQuestionEditorError(
                "Добавь текст вопроса и эталонный ответ.");
            return;
        }

        string? promptPath =
            viewModel.RelatedQuestionPromptPath;

        string? answerPath =
            viewModel.RelatedQuestionAnswerPath;

        if (string.IsNullOrWhiteSpace(promptPath) ||
            string.IsNullOrWhiteSpace(answerPath) ||
            !File.Exists(promptPath) ||
            !File.Exists(answerPath))
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не найдены Markdown-файлы вопроса или эталонного ответа.");
            return;
        }

        try
        {
            string promptMarkdown =
                await ReadMarkdownSnapshotAsync(promptPath);

            string answerMarkdown =
                await ReadMarkdownSnapshotAsync(answerPath);

            var contentResult =
                QuestionContent.Create(
                    promptMarkdown,
                    answerMarkdown);

            if (contentResult.IsFailure)
            {
                viewModel.SetRelatedQuestionEditorError(
                    "Заполни текст вопроса и эталонный ответ в Markdown-файлах.");
                return;
            }

            viewModel.SaveRelatedQuestionDraft();
            DialogResult = true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException
                      or DecoderFallbackException)
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось прочитать Markdown-файлы. Сохрани изменения в редакторе и попробуй снова.");
        }
    }

    private void Window_OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;

        if (DataContext is CreateMaterialViewModel viewModel)
        {
            viewModel.CancelRelatedQuestionEditor();
        }

        DialogResult = false;
    }

    private static bool TryGetMarkdownSource(
        FrameworkElement element,
        out string source)
    {
        source = element.Tag as string ?? string.Empty;
        return source is PromptSource or AnswerSource;
    }

    private static bool TryGetSingleMarkdownFile(
        IDataObject data,
        out string path)
    {
        path = string.Empty;

        if (!data.GetDataPresent(DataFormats.FileDrop) ||
            data.GetData(DataFormats.FileDrop) is not string[] files ||
            files.Length != 1)
        {
            return false;
        }

        string candidate = files[0];
        if (!string.Equals(
                Path.GetExtension(candidate),
                ".md",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static async Task CopyFileAllowSharedReadAsync(
        string sourcePath,
        string destinationPath)
    {
        await using var sourceStream =
            new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 81920,
                useAsync: true);

        await using var destinationStream =
            new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

        await sourceStream.CopyToAsync(destinationStream);
    }

    private static async Task<string> ReadMarkdownSnapshotAsync(
        string path)
    {
        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

        using var reader =
            new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);

        return await reader.ReadToEndAsync();
    }

    private static string GetTemplateContent(
        string source) =>
        source switch
        {
            PromptSource =>
                "## Вопрос\n\n",
            AnswerSource =>
                "## Эталонный ответ\n\n",
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                null),
        };

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        DependencyObject? current = source;

        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
