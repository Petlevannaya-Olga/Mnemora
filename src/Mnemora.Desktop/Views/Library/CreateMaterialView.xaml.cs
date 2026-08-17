using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView : UserControl
{
    private const string ArticleSource = "Article";
    private const string QuestionSource = "Question";
    private const string AnswerSource = "Answer";

    private string? _articleMarkdownPath;
    private string? _questionMarkdownPath;
    private string? _answerMarkdownPath;

    public CreateMaterialView()
    {
        InitializeComponent();
    }

    private void GoToBasicStep_OnClick(object sender, RoutedEventArgs e)
    {
        WizardTabs.SelectedIndex = 1;
    }

    private void GoToTypeStep_OnClick(object sender, RoutedEventArgs e)
    {
        WizardTabs.SelectedIndex = 0;
    }

    private void MarkdownDropZone_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        // Кнопки состояния «файл выбран» имеют собственные действия.
        // Не открываем диалог выбора повторно, если клик пришёл от них.
        if (e.OriginalSource is DependencyObject originalSource &&
            FindVisualParent<Button>(originalSource) is not null)
        {
            return;
        }

        ChooseMarkdownFile(source);
        e.Handled = true;
    }


    private void ClearSelectedMarkdown_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        ClearSelectedFile(source);
        e.Handled = true;
    }

    private void OpenSelectedMarkdown_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        string? path = GetSelectedPath(source);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ShowFileError(source, "Файл больше не найден. Выберите его снова.");
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
            ShowFileError(source, "Не удалось открыть файл в системном редакторе.");
        }

        e.Handled = true;
    }

    private void MarkdownDropZone_OnDragEnter(object sender, DragEventArgs e)
    {
        UpdateDragState(sender, e);
    }

    private void MarkdownDropZone_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateDragState(sender, e);
    }

    private void MarkdownDropZone_OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element &&
            TryGetSource(element, out string source))
        {
            ResetDropZoneVisual(source);
        }

        e.Handled = true;
    }

    private void MarkdownDropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        ResetDropZoneVisual(source);

        string[] files = GetDroppedFiles(e.Data);

        if (files.Length != 1)
        {
            ShowFileError(source, "Перетащите один Markdown-файл.");
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!TryValidateMarkdownFile(files[0], out string? error))
        {
            ShowFileError(source, error ?? "Не удалось выбрать файл.");
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        SetSelectedFile(source, files[0]);
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void UpdateDragState(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        string[] files = GetDroppedFiles(e.Data);
        bool isValid = files.Length == 1 &&
                       TryValidateMarkdownFile(files[0], out _);

        e.Effects = isValid
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        SetDropZoneHighlight(source, isValid);
        e.Handled = true;
    }

    private void ChooseMarkdownFile(string source)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите Markdown-файл",
            Filter = "Markdown (*.md)|*.md",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!TryValidateMarkdownFile(dialog.FileName, out string? error))
        {
            ShowFileError(source, error ?? "Не удалось выбрать файл.");
            return;
        }

        SetSelectedFile(source, dialog.FileName);
    }

    private void SetSelectedFile(string source, string path)
    {
        string fullPath = Path.GetFullPath(path);
        SetSelectedPath(source, fullPath);

        var controls = GetSourceControls(source);
        controls.EmptyState.Visibility = Visibility.Collapsed;
        controls.SelectedState.Visibility = Visibility.Visible;
        controls.ClearButton.Visibility = Visibility.Visible;
        controls.SelectedFileName.Text = Path.GetFileName(fullPath);
        controls.SelectedFileName.ToolTip = fullPath;

        HideFileError(source);
        ResetDropZoneVisual(source);
    }

    private void ClearSelectedFile(string source)
    {
        SetSelectedPath(source, null);

        var controls = GetSourceControls(source);
        controls.SelectedState.Visibility = Visibility.Collapsed;
        controls.ClearButton.Visibility = Visibility.Collapsed;
        controls.EmptyState.Visibility = Visibility.Visible;
        controls.SelectedFileName.Text = string.Empty;
        controls.SelectedFileName.ToolTip = null;

        HideFileError(source);
        ResetDropZoneVisual(source);
    }

    private static bool TryValidateMarkdownFile(string path, out string? error)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = "Файл не найден.";
            return false;
        }

        if (!string.Equals(
                Path.GetExtension(path),
                ".md",
                StringComparison.OrdinalIgnoreCase))
        {
            error = "Можно выбрать только файл .md.";
            return false;
        }

        error = null;
        return true;
    }

    private static string[] GetDroppedFiles(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop))
        {
            return [];
        }

        return data.GetData(DataFormats.FileDrop) as string[] ?? [];
    }

    private void SetDropZoneHighlight(string source, bool isValid)
    {
        Rectangle border = GetSourceControls(source).DropBorder;
        border.StrokeThickness = 2;

        if (!isValid)
        {
            border.Stroke = new SolidColorBrush(Color.FromRgb(209, 67, 67));
            return;
        }

        string accentResource = source == AnswerSource
            ? "Mnemora.Brush.Primary"
            : "Mnemora.Brush.Secondary";

        border.Stroke = TryFindResource(accentResource) as Brush ?? Brushes.MediumPurple;
    }

    private void ResetDropZoneVisual(string source)
    {
        Rectangle border = GetSourceControls(source).DropBorder;
        border.SetResourceReference(Shape.StrokeProperty, "Mnemora.Brush.Border");
        border.SetResourceReference(Shape.FillProperty, "Mnemora.Brush.SurfaceMuted");
        border.StrokeThickness = 1.2;
    }

    private void ShowFileError(string source, string message)
    {
        TextBlock errorText = GetSourceControls(source).ErrorText;
        errorText.Text = message;
        errorText.Visibility = Visibility.Visible;
    }

    private void HideFileError(string source)
    {
        TextBlock errorText = GetSourceControls(source).ErrorText;
        errorText.Text = string.Empty;
        errorText.Visibility = Visibility.Collapsed;
    }

    private string? GetSelectedPath(string source) =>
        source switch
        {
            ArticleSource => _articleMarkdownPath,
            QuestionSource => _questionMarkdownPath,
            AnswerSource => _answerMarkdownPath,
            _ => null,
        };

    private void SetSelectedPath(string source, string? path)
    {
        switch (source)
        {
            case ArticleSource:
                _articleMarkdownPath = path;
                break;
            case QuestionSource:
                _questionMarkdownPath = path;
                break;
            case AnswerSource:
                _answerMarkdownPath = path;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, null);
        }
    }

    private (StackPanel EmptyState,
             Grid SelectedState,
             TextBlock SelectedFileName,
             TextBlock ErrorText,
             Rectangle DropBorder,
             Button ClearButton) GetSourceControls(string source) =>
        source switch
        {
            ArticleSource => (
                ArticleEmptyState,
                ArticleSelectedState,
                ArticleSelectedFileName,
                ArticleFileError,
                ArticleDropBorder,
                ArticleClearFileButton),

            QuestionSource => (
                QuestionEmptyState,
                QuestionSelectedState,
                QuestionSelectedFileName,
                QuestionFileError,
                QuestionDropBorder,
                QuestionClearFileButton),

            AnswerSource => (
                AnswerEmptyState,
                AnswerSelectedState,
                AnswerSelectedFileName,
                AnswerFileError,
                AnswerDropBorder,
                AnswerClearFileButton),

            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

    private static bool TryGetSource(FrameworkElement element, out string source)
    {
        source = element.Tag as string ?? string.Empty;
        return source is ArticleSource or QuestionSource or AnswerSource;
    }

    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        DependencyObject? current = child;

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
