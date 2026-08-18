using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Domain.Materials;
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
        DataContextChanged += CreateMaterialView_OnDataContextChanged;
    }

    private void CreateMaterialView_OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CreateMaterialViewModel oldViewModel)
        {
            oldViewModel.Closing -= CreateMaterialViewModel_OnClosing;
        }

        if (e.NewValue is CreateMaterialViewModel newViewModel)
        {
            newViewModel.Closing += CreateMaterialViewModel_OnClosing;
        }
    }

    private void CreateMaterialViewModel_OnClosing(
        object? sender,
        EventArgs e)
    {
        // Все созданные Mnemora временные файлы живут до закрытия мастера.
        // Это позволяет безопасно оставить старый черновик открытым
        // в VS Code / Obsidian, даже если пользователь создал новый.
        DeleteAllOwnedDrafts();

        ClearSelectedFile(ArticleSource);
        ClearSelectedFile(QuestionSource);
        ClearSelectedFile(AnswerSource);

        HideMaterialStepError();

        if (FindName("ReviewMaterialPreview") is MaterialPreviewView preview)
        {
            preview.Title = string.Empty;
            preview.TopicName = string.Empty;
            preview.Difficulty = string.Empty;
            preview.Tags = Array.Empty<string>();
            preview.BodyMarkdown = string.Empty;
            preview.ReferenceAnswerMarkdown = string.Empty;
        }

        FindRequiredControl<TabControl>("WizardTabs").SelectedIndex = 0;
    }

    private void GoToBasicStep_OnClick(object sender, RoutedEventArgs e)
    {
        FindRequiredControl<TabControl>("WizardTabs").SelectedIndex = 1;
    }

    private void GoToTypeStep_OnClick(object sender, RoutedEventArgs e)
    {
        FindRequiredControl<TabControl>("WizardTabs").SelectedIndex = 0;
    }

    private async void GoToReviewStep_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (!await TryPrepareReviewAsync())
        {
            return;
        }

        FindRequiredControl<TabControl>(
            "WizardTabs").SelectedIndex = 2;
    }

    private void GoToMaterialStep_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        HideMaterialStepError();

        FindRequiredControl<TabControl>(
            "WizardTabs").SelectedIndex = 1;
    }

    private async void GoToLearningStep_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        // Шаг 4 не должен быть доступен, если обязательные Markdown-файлы
        // отсутствуют или были удалены после открытия шага проверки.
        // Повторно используем ту же проверку, что и при переходе 2 -> 3,
        // чтобы у мастера был единый набор предусловий.
        if (!await TryPrepareReviewAsync())
        {
            FindRequiredControl<TabControl>(
                "WizardTabs").SelectedIndex = 1;

            return;
        }

        if (DataContext
            is not CreateMaterialViewModel viewModel)
        {
            return;
        }

        bool isQuestion =
            FindRequiredControl<RadioButton>(
                    "QuestionChoiceRadio")
                .IsChecked == true;

        await viewModel
            .LoadLearningOptionsAsync(
                isQuestion);

        FindRequiredControl<TabControl>(
            "WizardTabs").SelectedIndex = 3;
    }

    private void GoToReviewFromLearningStep_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        FindRequiredControl<TabControl>(
            "WizardTabs").SelectedIndex = 2;
    }

    private void GoToExperienceStep_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is CreateMaterialViewModel viewModel &&
            !viewModel.CanProceedFromLinks)
        {
            return;
        }

        FindRequiredControl<TabControl>(
            "WizardTabs").SelectedIndex = 4;
    }

    private void GoToLinksFromExperienceStep_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        FindRequiredControl<TabControl>(
            "WizardTabs").SelectedIndex = 3;
    }

    private async Task<bool> TryPrepareReviewAsync()
    {
        HideMaterialStepError();

        TextBox titleInput =
            FindRequiredControl<TextBox>(
                "MaterialTitleInput");

        string title =
            titleInput.Text.Trim();

        if (title.Length is
            < MaterialTitle.MinLength
            or > MaterialTitle.MaxLength)
        {
            ShowMaterialStepError(
                $"Название должно содержать от {MaterialTitle.MinLength} до {MaterialTitle.MaxLength} символов.");

            titleInput.Focus();
            return false;
        }

        ComboBox difficultyComboBox =
            FindRequiredControl<ComboBox>(
                "DifficultyComboBox");

        string difficulty =
            (difficultyComboBox.SelectedItem
                as ComboBoxItem)?
            .Content?
            .ToString()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(
                difficulty))
        {
            ShowMaterialStepError(
                "Выберите сложность материала.");

            difficultyComboBox.Focus();
            return false;
        }

        RadioButton articleChoice =
            FindRequiredControl<RadioButton>(
                "ArticleChoiceRadio");

        RadioButton questionChoice =
            FindRequiredControl<RadioButton>(
                "QuestionChoiceRadio");

        bool isArticle =
            articleChoice.IsChecked == true;

        bool isQuestion =
            questionChoice.IsChecked == true;

        if (!isArticle &&
            !isQuestion)
        {
            ShowMaterialStepError(
                "Сначала выберите тип материала.");

            FindRequiredControl<TabControl>(
                "WizardTabs").SelectedIndex = 0;

            return false;
        }

        string? bodyPath =
            isArticle
                ? GetSelectedPath(
                    ArticleSource)
                : GetSelectedPath(
                    QuestionSource);

        string bodySource =
            isArticle
                ? ArticleSource
                : QuestionSource;

        if (string.IsNullOrWhiteSpace(
                bodyPath) ||
            !File.Exists(
                bodyPath))
        {
            string message =
                isArticle
                    ? "Выберите Markdown-файл статьи."
                    : "Выберите Markdown-файл вопроса.";

            ShowFileError(
                bodySource,
                message);

            ShowMaterialStepError(
                message);

            return false;
        }

        string? answerPath =
            isQuestion
                ? GetSelectedPath(
                    AnswerSource)
                : null;

        if (isQuestion &&
            (string.IsNullOrWhiteSpace(
                 answerPath) ||
             !File.Exists(
                 answerPath)))
        {
            const string message =
                "Выберите Markdown-файл эталонного ответа.";

            ShowFileError(
                AnswerSource,
                message);

            ShowMaterialStepError(
                message);

            return false;
        }

        try
        {
            // ВАЖНО: читаем файлы заново при каждом переходе на шаг 3.
            // Поэтому правки, сохранённые в Obsidian/VS Code после возврата
            // на шаг 2, всегда попадают в финальную проверку.
            string bodyMarkdown =
                await ReadMarkdownSnapshotAsync(
                    bodyPath);

            string answerMarkdown =
                isQuestion
                    ? await ReadMarkdownSnapshotAsync(
                        answerPath!)
                    : string.Empty;

            if (DataContext
                is not CreateMaterialViewModel viewModel)
            {
                ShowMaterialStepError(
                    "Не удалось получить состояние создаваемого материала.");

                return false;
            }

            var preview =
                FindRequiredControl<
                    MaterialPreviewView>(
                    "ReviewMaterialPreview");

            preview.Title = title;
            preview.TypeLabel =
                isQuestion
                    ? "Вопрос"
                    : "Статья";
            preview.IsQuestion =
                isQuestion;
            preview.TopicName =
                viewModel.SelectedTopic?.Name
                ?? string.Empty;
            preview.Difficulty =
                difficulty;
            preview.IconKind =
                viewModel.SelectedIconKind;
            preview.Tags =
                GetTags(
                    FindRequiredControl<WrapPanel>(
                        "TagsPanel"));
            preview.BodyMarkdown =
                bodyMarkdown;
            preview.ReferenceAnswerMarkdown =
                answerMarkdown;

            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException
                      or DecoderFallbackException)
        {
            ShowMaterialStepError(
                "Не удалось прочитать Markdown-файл. Сохраните изменения в редакторе и попробуйте снова.");

            return false;
        }
    }

    private static async Task<string> ReadMarkdownSnapshotAsync(
        string path)
    {
        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
                | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

        using var reader =
            new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);

        return await reader.ReadToEndAsync();
    }

    private void ShowMaterialStepError(
        string message)
    {
        TextBlock error =
            FindRequiredControl<TextBlock>(
                "MaterialStepError");

        error.Text = message;
        error.Visibility =
            Visibility.Visible;
    }

    private void HideMaterialStepError()
    {
        TextBlock error =
            FindRequiredControl<TextBlock>(
                "MaterialStepError");

        error.Text = string.Empty;
        error.Visibility =
            Visibility.Collapsed;
    }

    private async void MarkdownDropZone_OnMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
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

        e.Handled = true;
        await ChooseMarkdownFileAsync(source);
    }


    private void ClearSelectedMarkdown_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        // Снимаем выбор, но не удаляем временный файл сразу:
        // он может оставаться открытым во внешнем редакторе.
        ClearSelectedFile(source);
        e.Handled = true;
    }

    private async void OpenSelectedMarkdown_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        e.Handled = true;

        string? path = GetSelectedPath(source);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ShowFileError(
                source,
                "Файл больше не найден. Выберите его снова.");
            return;
        }

        if (DataContext is not CreateMaterialViewModel viewModel)
        {
            ShowFileError(
                source,
                "Не удалось определить настройки Markdown-редактора.");
            return;
        }

        try
        {
            MarkdownEditorLaunchResult result =
                await viewModel.OpenMarkdownAsync(path);

            if (!result.IsSuccess)
            {
                ShowFileError(source, result.Message);
                return;
            }

            HideFileError(source);
        }
        catch (OperationCanceledException)
        {
            // Пользователь закрыл мастер или операция была отменена.
        }
        catch (Exception)
        {
            ShowFileError(
                source,
                "Не удалось открыть файл в настроенном Markdown-редакторе.");
        }
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

    private async void MarkdownDropZone_OnDrop(
        object sender,
        DragEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            !TryGetSource(element, out string source))
        {
            return;
        }

        e.Handled = true;
        ResetDropZoneVisual(source);

        string[] files = GetDroppedFiles(e.Data);

        if (files.Length != 1)
        {
            ShowFileError(source, "Перетащите один Markdown-файл.");
            e.Effects = DragDropEffects.None;
            return;
        }

        if (!TryValidateMarkdownFile(files[0], out string? error))
        {
            ShowFileError(source, error ?? "Не удалось выбрать файл.");
            e.Effects = DragDropEffects.None;
            return;
        }

        bool imported =
            await ImportAndSelectMarkdownAsync(
                source,
                files[0]);

        e.Effects = imported
            ? DragDropEffects.Copy
            : DragDropEffects.None;
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

    private async Task ChooseMarkdownFileAsync(
        string source)
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

        await ImportAndSelectMarkdownAsync(
            source,
            dialog.FileName);
    }

    private async Task<bool> ImportAndSelectMarkdownAsync(
        string source,
        string sourcePath)
    {
        try
        {
            string importedPath =
                await ImportMarkdownIntoDraftAsync(
                    source,
                    sourcePath);

            // Импортированная копия принадлежит Mnemora.
            // Исходный пользовательский файл не трогаем.
            RegisterOwnedDraft(importedPath);

            // Предыдущий временный файл не удаляем: он может быть открыт
            // в редакторе. Новый импорт просто становится текущим.
            SetSelectedFile(source, importedPath);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException
                      or PathTooLongException)
        {
            ShowFileError(
                source,
                "Не удалось скопировать Markdown-файл в хранилище Mnemora.");
            return false;
        }
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

        UpdateTemplateActionText(
            source,
            hasSelectedFile: true);

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

        UpdateTemplateActionText(
            source,
            hasSelectedFile: false);

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

    private void UpdateTemplateActionText(
        string source,
        bool hasSelectedFile)
    {
        TextBlock title = source switch
        {
            ArticleSource =>
                FindRequiredControl<TextBlock>(
                    "ArticleCreateTemplateTitle"),
            QuestionSource =>
                FindRequiredControl<TextBlock>(
                    "QuestionCreateTemplateTitle"),
            AnswerSource =>
                FindRequiredControl<TextBlock>(
                    "AnswerCreateTemplateTitle"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                null),
        };

        TextBlock subtitle = source switch
        {
            ArticleSource =>
                FindRequiredControl<TextBlock>(
                    "ArticleCreateTemplateSubtitle"),
            QuestionSource =>
                FindRequiredControl<TextBlock>(
                    "QuestionCreateTemplateSubtitle"),
            AnswerSource =>
                FindRequiredControl<TextBlock>(
                    "AnswerCreateTemplateSubtitle"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                null),
        };

        title.Text = hasSelectedFile
            ? "Создать новый"
            : "Создать по шаблону";

        subtitle.Text = hasSelectedFile
            ? "Заменить текущий .md"
            : "Новый .md для редактора";
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
                FindRequiredControl<StackPanel>("ArticleEmptyState"),
                FindRequiredControl<Grid>("ArticleSelectedState"),
                FindRequiredControl<TextBlock>("ArticleSelectedFileName"),
                FindRequiredControl<TextBlock>("ArticleFileError"),
                FindRequiredControl<Rectangle>("ArticleDropBorder"),
                FindRequiredControl<Button>("ArticleClearFileButton")),

            QuestionSource => (
                FindRequiredControl<StackPanel>("QuestionEmptyState"),
                FindRequiredControl<Grid>("QuestionSelectedState"),
                FindRequiredControl<TextBlock>("QuestionSelectedFileName"),
                FindRequiredControl<TextBlock>("QuestionFileError"),
                FindRequiredControl<Rectangle>("QuestionDropBorder"),
                FindRequiredControl<Button>("QuestionClearFileButton")),

            AnswerSource => (
                FindRequiredControl<StackPanel>("AnswerEmptyState"),
                FindRequiredControl<Grid>("AnswerSelectedState"),
                FindRequiredControl<TextBlock>("AnswerSelectedFileName"),
                FindRequiredControl<TextBlock>("AnswerFileError"),
                FindRequiredControl<Rectangle>("AnswerDropBorder"),
                FindRequiredControl<Button>("AnswerClearFileButton")),

            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };


    private T FindRequiredControl<T>(string name)
        where T : FrameworkElement
    {
        if (FindName(name) is T control)
        {
            return control;
        }

        throw new InvalidOperationException(
            $"Элемент '{name}' типа {typeof(T).Name} не найден в CreateMaterialView.xaml.");
    }

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
