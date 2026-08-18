using System.IO;
using System.Windows;
using System.Windows.Controls;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Domain.Materials;

namespace Mnemora.Desktop.Views.Library;

public partial class CreateMaterialView
{
    private async void OpenExistingQuestionsPicker_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel ||
            viewModel.SelectedTopic is null)
        {
            return;
        }

        IReadOnlyList<Mnemora.Contracts.StandaloneQuestionPickerOptionDto>? options =
            await viewModel.LoadStandaloneQuestionPickerOptionsAsync();

        if (options is null)
        {
            return;
        }

        var pickerViewModel =
            new RelatedQuestionPickerViewModel(
                options,
                viewModel.SelectedLinkedQuestionIds,
                viewModel.SelectedTopic.Id,
                viewModel.SelectedTopic.Name);

        var dialog =
            new RelatedQuestionPickerWindow(
                pickerViewModel);

        Window? owner = Window.GetWindow(this);

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
            if (dialog.ShowDialog() == true)
            {
                viewModel.ApplyStandaloneQuestionSelection(
                    pickerViewModel.GetSelectedOptions());
            }
        }
        finally
        {
            overlayHost?.HideDialogOverlay();
        }
    }

    private void RemoveStandaloneQuestion_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not CreateMaterialViewModel viewModel ||
            sender is not FrameworkElement element ||
            element.Tag is not LearningQuestionOptionViewModel question)
        {
            return;
        }

        viewModel.RemoveStandaloneQuestion(question);
    }

    private async void OpenNewRelatedQuestion_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext
            is not CreateMaterialViewModel viewModel)
        {
            return;
        }

        viewModel.BeginNewRelatedQuestion(
            GetCurrentMaterialDifficulty());

        try
        {
            string rootDirectory =
                await GetTemplateDirectoryAsync();

            string questionDirectory =
                Path.Combine(
                    rootDirectory,
                    "related-questions",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                questionDirectory);

            string promptPath =
                Path.Combine(
                    questionDirectory,
                    "question.md");

            string answerPath =
                Path.Combine(
                    questionDirectory,
                    "answer.md");

            await File.WriteAllTextAsync(
                promptPath,
                string.Empty);

            await File.WriteAllTextAsync(
                answerPath,
                string.Empty);

            RegisterOwnedDraft(promptPath);
            RegisterOwnedDraft(answerPath);

            viewModel.SetRelatedQuestionDraftFiles(
                promptPath,
                answerPath);
        }
        catch (OperationCanceledException)
        {
            viewModel.CancelRelatedQuestionEditor();
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException
                      or PathTooLongException)
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось создать временные Markdown-файлы вопроса.");
        }

        if (viewModel.IsRelatedQuestionEditorOpen)
        {
            ShowRelatedQuestionEditor(viewModel);
        }
    }

    private void EditRelatedQuestion_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext
                is not CreateMaterialViewModel viewModel ||
            sender is not FrameworkElement element ||
            element.Tag
                is not RelatedQuestionDraftViewModel draft)
        {
            return;
        }

        viewModel.BeginEditRelatedQuestion(
            draft);

        ShowRelatedQuestionEditor(viewModel);
    }

    private void RemoveRelatedQuestion_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext
                is not CreateMaterialViewModel viewModel ||
            sender is not FrameworkElement element ||
            element.Tag
                is not RelatedQuestionDraftViewModel draft)
        {
            return;
        }

        viewModel.RemoveRelatedQuestionDraft(
            draft);
    }

    private void CancelRelatedQuestionEditor_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext
            is CreateMaterialViewModel viewModel)
        {
            viewModel.CancelRelatedQuestionEditor();
        }
    }

    private async void OpenRelatedQuestionMarkdown_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext
                is not CreateMaterialViewModel viewModel ||
            sender is not FrameworkElement element)
        {
            return;
        }

        string? path =
            string.Equals(
                element.Tag as string,
                "Answer",
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
                viewModel.SetRelatedQuestionEditorError(
                    result.Message);
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

    private async void SaveRelatedQuestionDraft_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        e.Handled = true;

        if (DataContext
            is not CreateMaterialViewModel viewModel)
        {
            return;
        }

        viewModel.SetRelatedQuestionEditorError(null);

        if (viewModel.HasRelatedQuestionValidationError)
        {
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
                await ReadMarkdownSnapshotAsync(
                    promptPath);

            string answerMarkdown =
                await ReadMarkdownSnapshotAsync(
                    answerPath);

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
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or ArgumentException
                      or NotSupportedException)
        {
            viewModel.SetRelatedQuestionEditorError(
                "Не удалось прочитать Markdown-файлы. Сохрани изменения в редакторе и попробуй снова.");
        }
    }


    private void ShowRelatedQuestionEditor(
        CreateMaterialViewModel viewModel)
    {
        var dialog =
            new RelatedQuestionEditorWindow
            {
                DataContext = viewModel,
            };

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
            dialog.ShowDialog();
        }
        finally
        {
            overlayHost?.HideDialogOverlay();

            // Закрытие окна крестиком/Alt+F4 не должно оставлять
            // мастер в состоянии открытого редактора.
            if (viewModel.IsRelatedQuestionEditorOpen)
            {
                viewModel.CancelRelatedQuestionEditor();
            }
        }
    }

    private MaterialDifficulty GetCurrentMaterialDifficulty()
    {
        ComboBox comboBox =
            FindRequiredControl<ComboBox>(
                "DifficultyComboBox");

        string value =
            (comboBox.SelectedItem
                as ComboBoxItem)?
            .Content?
            .ToString()
            ?? string.Empty;

        return value switch
        {
            "Начальный" =>
                MaterialDifficulty.Easy,
            "Продвинутый" =>
                MaterialDifficulty.Hard,
            _ =>
                MaterialDifficulty.Medium,
        };
    }
}
