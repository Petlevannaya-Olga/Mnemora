using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Mnemora.Application.Materials.CreateGraph;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Domain.Materials;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView
{
    private async void GoToSummaryStep_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel ||
            !viewModel.CanProceedFromExperience)
        {
            return;
        }

        // Перед итогом ещё раз проверяем базовые данные и Markdown-файлы.
        // Это ловит ситуацию, когда файл удалили/переместили после шага 3.
        if (!await TryPrepareReviewAsync())
        {
            FindRequiredControl<TabControl>(
                "WizardTabs").SelectedIndex = 1;
            return;
        }

        bool isQuestion =
            FindRequiredControl<RadioButton>(
                    "QuestionChoiceRadio")
                .IsChecked == true;

        string title =
            FindRequiredControl<TextBox>(
                    "MaterialTitleInput")
                .Text
                .Trim();

        string difficulty =
            (FindRequiredControl<ComboBox>(
                    "DifficultyComboBox")
                .SelectedItem as ComboBoxItem)?
            .Content?
            .ToString()
            ?? string.Empty;

        IReadOnlyList<string> tags =
            GetTags(
                FindRequiredControl<WrapPanel>(
                    "TagsPanel"));

        string? bodyPath =
            GetSelectedPath(
                isQuestion
                    ? QuestionSource
                    : ArticleSource);

        string? answerPath =
            isQuestion
                ? GetSelectedPath(AnswerSource)
                : null;

        if (string.IsNullOrWhiteSpace(bodyPath))
        {
            FindRequiredControl<TabControl>(
                "WizardTabs").SelectedIndex = 1;
            return;
        }

        viewModel.PrepareSummary(
            title,
            difficulty,
            tags,
            bodyPath,
            answerPath);

        FindRequiredControl<TabControl>(
            "WizardTabs").SelectedIndex = 5;
    }

    private async void OpenSummaryMaterialPreview_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel)
        {
            return;
        }

        string? bodyPath =
            GetSelectedPath(
                viewModel.IsQuestionMaterial
                    ? QuestionSource
                    : ArticleSource);

        string? answerPath =
            viewModel.IsQuestionMaterial
                ? GetSelectedPath(AnswerSource)
                : null;

        if (string.IsNullOrWhiteSpace(bodyPath) ||
            !File.Exists(bodyPath))
        {
            viewModel.SetSummaryError(
                "Не удалось открыть просмотр: Markdown-файл материала не найден.");
            return;
        }

        if (viewModel.IsQuestionMaterial &&
            (string.IsNullOrWhiteSpace(answerPath) ||
             !File.Exists(answerPath)))
        {
            viewModel.SetSummaryError(
                "Не удалось открыть просмотр: Markdown-файл эталонного ответа не найден.");
            return;
        }

        try
        {
            string bodyMarkdown =
                await ReadMarkdownSnapshotAsync(bodyPath);

            string answerMarkdown =
                viewModel.IsQuestionMaterial
                    ? await ReadMarkdownSnapshotAsync(answerPath!)
                    : string.Empty;

            MaterialPreviewView preview =
                FindRequiredControl<MaterialPreviewView>(
                    "SummaryFullscreenPreview");

            preview.Title = viewModel.SummaryTitle;
            preview.TypeLabel = viewModel.SummaryMaterialTypeText;
            preview.TopicName = viewModel.SummaryTopicName;
            preview.Difficulty = viewModel.SummaryDifficultyText;
            preview.IconKind = viewModel.SelectedIconKind;
            preview.IsQuestion = viewModel.IsQuestionMaterial;

            // Связанный вопрос не хранит собственные теги. Пока в picker статьи
            // не загружаются её теги, не подменяем их устаревшими тегами вопроса.
            preview.Tags =
                viewModel.IsQuestionMaterial && viewModel.HasSelectedArticle
                    ? Array.Empty<string>()
                    : viewModel.SummaryTags;

            preview.BodyMarkdown = bodyMarkdown;
            preview.ReferenceAnswerMarkdown = answerMarkdown;

            viewModel.SetSummaryError(null);
            preview.OpenFullscreen();
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException
                      or DecoderFallbackException)
        {
            viewModel.SetSummaryError(
                "Не удалось открыть просмотр. Сохраните Markdown-файлы и попробуйте снова.");
        }
    }


    private async void CreateSummaryMaterial_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel ||
            viewModel.IsSummaryCreating ||
            viewModel.SelectedTopic is null)
        {
            return;
        }

        // Финальный клик снова проверяет Markdown. Между открытием «Итога»
        // и созданием файл мог быть удалён или изменён во внешнем редакторе.
        if (!await TryPrepareReviewAsync())
        {
            FindRequiredControl<TabControl>(
                "WizardTabs").SelectedIndex = 1;
            return;
        }

        bool isQuestion = viewModel.IsQuestionMaterial;

        string? bodyPath =
            GetSelectedPath(
                isQuestion
                    ? QuestionSource
                    : ArticleSource);

        string? answerPath =
            isQuestion
                ? GetSelectedPath(AnswerSource)
                : null;

        if (string.IsNullOrWhiteSpace(bodyPath) ||
            !File.Exists(bodyPath))
        {
            viewModel.SetSummaryError(
                "Markdown-файл материала больше не найден.");
            return;
        }

        try
        {
            string bodyMarkdown =
                await ReadMarkdownSnapshotAsync(bodyPath);

            string? answerMarkdown = null;

            if (isQuestion)
            {
                if (string.IsNullOrWhiteSpace(answerPath) ||
                    !File.Exists(answerPath))
                {
                    viewModel.SetSummaryError(
                        "Markdown-файл эталонного ответа больше не найден.");
                    return;
                }

                answerMarkdown =
                    await ReadMarkdownSnapshotAsync(answerPath);
            }

            var newQuestions =
                new List<CreateMaterialGraphQuestionDraft>(
                    viewModel.NewRelatedQuestions.Count);

            foreach (RelatedQuestionDraftViewModel draft
                     in viewModel.NewRelatedQuestions)
            {
                if (draft.PromptPath is not { Length: > 0 } promptPath ||
                    draft.ReferenceAnswerPath is not { Length: > 0 } referenceAnswerPath ||
                    !File.Exists(promptPath) ||
                    !File.Exists(referenceAnswerPath))
                {
                    viewModel.SetSummaryError(
                        $"Не удалось создать вопрос «{draft.Title}»: один из Markdown-файлов больше не найден.");
                    return;
                }

                string promptMarkdown =
                    await ReadMarkdownSnapshotAsync(promptPath);

                string referenceAnswerMarkdown =
                    await ReadMarkdownSnapshotAsync(
                        referenceAnswerPath);

                newQuestions.Add(
                    new CreateMaterialGraphQuestionDraft(
                        draft.Title,
                        draft.Difficulty,
                        draft.IconKey,
                        draft.StudyPoints,
                        draft.ReviewPoints,
                        promptMarkdown,
                        referenceAnswerMarkdown));
            }

            MaterialDifficulty difficulty =
                FindRequiredControl<ComboBox>(
                        "DifficultyComboBox")
                    .SelectedIndex switch
                    {
                        0 => MaterialDifficulty.Easy,
                        1 => MaterialDifficulty.Medium,
                        2 => MaterialDifficulty.Hard,
                        _ => MaterialDifficulty.Medium,
                    };

            var command =
                new CreateMaterialGraphCommand(
                    viewModel.SelectedTopic.Id,
                    isQuestion
                        ? MaterialType.Question
                        : MaterialType.Article,
                    viewModel.SummaryTitle,
                    difficulty,
                    viewModel.SelectedIconKey,
                    viewModel.StudyPoints,
                    viewModel.ReviewPoints,
                    bodyMarkdown,
                    answerMarkdown,
                    viewModel.SummaryTags.ToArray(),
                    viewModel.SelectedArticleId,
                    isQuestion
                        ? Array.Empty<Guid>()
                        : viewModel.SelectedLinkedQuestionIds,
                    isQuestion
                        ? Array.Empty<CreateMaterialGraphQuestionDraft>()
                        : newQuestions);

            await viewModel.CreateSummaryMaterialAsync(command);
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException
                      or DecoderFallbackException)
        {
            viewModel.SetSummaryError(
                "Не удалось прочитать один из Markdown-файлов. Сохраните файлы и попробуйте снова.");
        }
    }

    private void GoToExperienceFromSummaryStep_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        FindRequiredControl<TabControl>(
            "WizardTabs").SelectedIndex = 4;
    }
}
