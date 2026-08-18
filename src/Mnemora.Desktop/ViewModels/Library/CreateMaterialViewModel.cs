using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Mnemora.Application.Materials.Learning.GetOptions;
using Mnemora.Application.Materials.Learning.Picker;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Topics;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class CreateMaterialViewModel(
    IMarkdownEditorService markdownEditorService,
    ISettingsService settingsService,
    IDialogService dialogService,
    IQueryDispatcher queryDispatcher)
    : ViewModelBase
{
    private const string MaterialsDirectoryName = "materials";
    private const string DraftsDirectoryName = "_drafts";
    private const int ExperiencePointsStep = 5;

    private const PackIconKind DefaultMaterialIconKind =
        PackIconKind.FileDocumentOutline;

    private const PackIconKind DefaultRelatedQuestionIconKind =
        PackIconKind.HelpCircleOutline;

    private Action? _closeRequested;

    [ObservableProperty]
    private LibraryManagementOrderItemViewModel? _selectedTopic;

    [ObservableProperty]
    private PackIconKind _selectedIconKind =
        DefaultMaterialIconKind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsArticleMaterial))]
    private bool _isQuestionMaterial;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLearningOptionsError))]
    [NotifyPropertyChangedFor(nameof(CanProceedFromLinks))]
    private string? _learningOptionsError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceedFromLinks))]
    private bool _isLearningOptionsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedArticleId))]
    private LearningArticleOptionViewModel? _selectedArticle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LearningValidationMessage))]
    [NotifyPropertyChangedFor(nameof(HasLearningValidationError))]
    [NotifyPropertyChangedFor(nameof(CanProceedFromExperience))]
    [NotifyPropertyChangedFor(nameof(ReviewMaximum))]
    private int _studyPoints = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LearningValidationMessage))]
    [NotifyPropertyChangedFor(nameof(HasLearningValidationError))]
    [NotifyPropertyChangedFor(nameof(CanProceedFromExperience))]
    private int _reviewPoints = 20;

    [ObservableProperty]
    private bool _isRelatedQuestionEditorOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldShowRelatedQuestionValidationError))]
    private bool _hasRelatedQuestionValidationAttempted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionValidationMessage))]
    [NotifyPropertyChangedFor(nameof(HasRelatedQuestionValidationError))]
    [NotifyPropertyChangedFor(nameof(ShouldShowRelatedQuestionValidationError))]
    [NotifyPropertyChangedFor(nameof(CanSaveRelatedQuestion))]
    private string _relatedQuestionTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveRelatedQuestion))]
    private MaterialDifficulty _relatedQuestionDifficulty =
        MaterialDifficulty.Medium;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionIconKey))]
    [NotifyPropertyChangedFor(nameof(CanSaveRelatedQuestion))]
    private PackIconKind _relatedQuestionIconKind =
        DefaultRelatedQuestionIconKind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionValidationMessage))]
    [NotifyPropertyChangedFor(nameof(HasRelatedQuestionValidationError))]
    [NotifyPropertyChangedFor(nameof(ShouldShowRelatedQuestionValidationError))]
    [NotifyPropertyChangedFor(nameof(CanSaveRelatedQuestion))]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionReviewMaximum))]
    private int _relatedQuestionStudyPoints = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionValidationMessage))]
    [NotifyPropertyChangedFor(nameof(HasRelatedQuestionValidationError))]
    [NotifyPropertyChangedFor(nameof(ShouldShowRelatedQuestionValidationError))]
    [NotifyPropertyChangedFor(nameof(CanSaveRelatedQuestion))]
    private int _relatedQuestionReviewPoints = 20;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionPromptFileName))]
    private string? _relatedQuestionPromptPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionAnswerFileName))]
    private string? _relatedQuestionAnswerPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveRelatedQuestion))]
    private bool _isRelatedQuestionPromptConfigured;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveRelatedQuestion))]
    private bool _isRelatedQuestionAnswerConfigured;

    [ObservableProperty]
    private string? _relatedQuestionEditorError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionEditorTitle))]
    [NotifyPropertyChangedFor(nameof(RelatedQuestionEditorActionText))]
    private RelatedQuestionDraftViewModel? _editingRelatedQuestion;

    public ObservableCollection<LearningQuestionOptionViewModel>
        AvailableQuestions { get; } = [];

    public ObservableCollection<LearningArticleOptionViewModel>
        AvailableArticles { get; } = [];

    public ObservableCollection<RelatedQuestionDraftViewModel>
        NewRelatedQuestions { get; } = [];

    public IReadOnlyList<RelatedQuestionDifficultyOption>
        RelatedQuestionDifficultyOptions { get; } =
        [
            new("Лёгкий", MaterialDifficulty.Easy),
            new("Средний", MaterialDifficulty.Medium),
            new("Сложный", MaterialDifficulty.Hard),
        ];

    public string SelectedIconKey =>
        SelectedIconKind.ToString();

    public string RelatedQuestionIconKey =>
        RelatedQuestionIconKind.ToString();

    public bool IsArticleMaterial =>
        !IsQuestionMaterial;

    public bool HasLearningOptionsError =>
        !string.IsNullOrWhiteSpace(
            LearningOptionsError);

    public bool HasVisibleQuestions =>
        AvailableQuestions.Count > 0;

    public bool HasAvailableArticles =>
        AvailableArticles.Any(article =>
            article.Id is not null);

    public int SelectedQuestionCount =>
        AvailableQuestions.Count;

    public string SelectedQuestionCountText =>
        SelectedQuestionCount == 0
            ? "Не выбрано"
            : FormatCount(
                SelectedQuestionCount,
                "вопрос",
                "вопроса",
                "вопросов");

    public int ArticleQuestionCount =>
        SelectedQuestionCount +
        NewRelatedQuestions.Count;

    public string ArticleQuestionCountText =>
        ArticleQuestionCount == 0
            ? "Не добавлено"
            : FormatCount(
                ArticleQuestionCount,
                "вопрос",
                "вопроса",
                "вопросов");

    public bool HasNewRelatedQuestions =>
        NewRelatedQuestions.Count > 0;

    public bool HasArticleQuestions =>
        ArticleQuestionCount > 0;

    public string RelatedQuestionPromptFileName =>
        string.IsNullOrWhiteSpace(RelatedQuestionPromptPath)
            ? "question.md"
            : Path.GetFileName(RelatedQuestionPromptPath);

    public string RelatedQuestionAnswerFileName =>
        string.IsNullOrWhiteSpace(RelatedQuestionAnswerPath)
            ? "answer.md"
            : Path.GetFileName(RelatedQuestionAnswerPath);

    public string RelatedQuestionEditorTitle =>
        EditingRelatedQuestion is null
            ? "Новый вопрос к статье"
            : "Изменить вопрос к статье";

    public string RelatedQuestionEditorActionText =>
        EditingRelatedQuestion is null
            ? "Добавить вопрос"
            : "Сохранить вопрос";

    public IReadOnlyList<Guid> SelectedLinkedQuestionIds =>
        AvailableQuestions
            .Select(question => question.Id)
            .ToArray();

    public Guid? SelectedArticleId =>
        SelectedArticle?.Id;

    public int RelatedQuestionReviewMaximum =>
        GetReviewMaximum(RelatedQuestionStudyPoints);

    public int ReviewMaximum =>
        GetReviewMaximum(StudyPoints);

    public string RelatedQuestionValidationMessage
    {
        get
        {
            var titleResult =
                MaterialTitle.Create(
                    RelatedQuestionTitle);

            if (titleResult.IsFailure)
            {
                return
                    $"Название вопроса должно содержать от {MaterialTitle.MinLength} " +
                    $"до {MaterialTitle.MaxLength} символов.";
            }

            var rewardsResult =
                MaterialExperienceRewards.Create(
                    RelatedQuestionStudyPoints,
                    RelatedQuestionReviewPoints);

            if (rewardsResult.IsFailure)
            {
                return rewardsResult.Error.Message;
            }

            return string.Empty;
        }
    }

    public bool HasRelatedQuestionValidationError =>
        !string.IsNullOrWhiteSpace(
            RelatedQuestionValidationMessage);

    public bool ShouldShowRelatedQuestionValidationError =>
        HasRelatedQuestionValidationAttempted &&
        HasRelatedQuestionValidationError;

    public bool CanSaveRelatedQuestion =>
        !HasRelatedQuestionValidationError &&
        IsRelatedQuestionPromptConfigured &&
        IsRelatedQuestionAnswerConfigured;

    public string LearningValidationMessage
    {
        get
        {
            if (StudyPoints is
                < MaterialExperienceRewards.MinPoints
                or > MaterialExperienceRewards.MaxPoints)
            {
                return
                    $"За изучение можно назначить от {MaterialExperienceRewards.MinPoints} " +
                    $"до {MaterialExperienceRewards.MaxPoints} XP.";
            }

            if (ReviewPoints is
                < MaterialExperienceRewards.MinPoints
                or > MaterialExperienceRewards.MaxPoints)
            {
                return
                    $"За повторение можно назначить от {MaterialExperienceRewards.MinPoints} " +
                    $"до {MaterialExperienceRewards.MaxPoints} XP.";
            }

            if (ReviewPoints >= StudyPoints)
            {
                return
                    "Опыт за повторение должен быть меньше опыта за первичное изучение.";
            }

            return string.Empty;
        }
    }

    public bool HasLearningValidationError =>
        !string.IsNullOrWhiteSpace(
            LearningValidationMessage);

    public bool CanProceedFromLinks =>
        !IsLearningOptionsLoading;

    public bool CanProceedFromExperience =>
        !HasLearningValidationError;

    public event EventHandler? Closing;

    public void Initialize(
        LibraryManagementOrderItemViewModel selectedTopic,
        Action closeRequested)
    {
        ArgumentNullException.ThrowIfNull(selectedTopic);
        ArgumentNullException.ThrowIfNull(closeRequested);

        SelectedTopic = selectedTopic;
        _closeRequested = closeRequested;

        ResetLearningState();
    }

    public void Reset()
    {
        SelectedTopic = null;
        SelectedIconKind = DefaultMaterialIconKind;
        _closeRequested = null;

        ResetLearningState();
    }

    partial void OnSelectedIconKindChanged(
        PackIconKind value)
    {
        OnPropertyChanged(
            nameof(SelectedIconKey));
    }


    partial void OnStudyPointsChanged(int value)
    {
        if (value <= MaterialExperienceRewards.MinPoints)
        {
            return;
        }

        if (ReviewPoints >= value)
        {
            ReviewPoints = ReviewMaximum;
        }
    }

    partial void OnReviewPointsChanged(int value)
    {
        int maximum = ReviewMaximum;

        if (value > maximum)
        {
            ReviewPoints = maximum;
        }
    }

    partial void OnRelatedQuestionStudyPointsChanged(int value)
    {
        if (value <= MaterialExperienceRewards.MinPoints)
        {
            return;
        }

        if (RelatedQuestionReviewPoints >= value)
        {
            RelatedQuestionReviewPoints = RelatedQuestionReviewMaximum;
        }
    }

    partial void OnRelatedQuestionReviewPointsChanged(int value)
    {
        int maximum = RelatedQuestionReviewMaximum;

        if (value > maximum)
        {
            RelatedQuestionReviewPoints = maximum;
        }
    }

    public Task<MarkdownEditorLaunchResult> OpenMarkdownAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return markdownEditorService.OpenAsync(
            filePath,
            cancellationToken);
    }

    public async Task<string> GetDraftDirectoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException(
                "Не указан идентификатор сессии создания материала.",
                nameof(sessionId));
        }

        AppSettings settings =
            await settingsService.LoadAsync(
                cancellationToken);

        if (string.IsNullOrWhiteSpace(settings.StoragePath))
        {
            throw new InvalidOperationException(
                "Хранилище Mnemora не настроено.");
        }

        string storagePath =
            Path.GetFullPath(
                settings.StoragePath.Trim());

        return Path.Combine(
            storagePath,
            MaterialsDirectoryName,
            DraftsDirectoryName,
            "create-material",
            sessionId);
    }

    public async Task LoadLearningOptionsAsync(
        bool isQuestionMaterial,
        CancellationToken cancellationToken = default)
    {
        IsQuestionMaterial =
            isQuestionMaterial;

        LearningOptionsError = null;

        // Для статьи готовые вопросы теперь выбираются отдельным глобальным
        // picker-окном. На входе в шаг «Связи» ничего загружать не нужно.
        if (!isQuestionMaterial)
        {
            IsLearningOptionsLoading = false;
            NotifyLearningCollectionsChanged();
            return;
        }

        IsLearningOptionsLoading = true;
        Guid? selectedArticleId =
            SelectedArticleId;

        try
        {
            if (SelectedTopic is null)
            {
                LearningOptionsError =
                    "Не удалось определить тему создаваемого материала.";

                ClearArticleOptions();
                return;
            }

            var result =
                await queryDispatcher.SendAsync<
                    GetMaterialLearningLinkOptionsQuery,
                    MaterialLearningLinkOptionsDto>(
                    new GetMaterialLearningLinkOptionsQuery(
                        SelectedTopic.Id),
                    cancellationToken);

            if (cancellationToken
                .IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                LearningOptionsError =
                    result.Error
                        .FirstOrDefault()
                        ?.Message
                    ?? "Не удалось загрузить статьи для настройки связи.";

                ClearArticleOptions();
                return;
            }

            LoadArticleOptions(
                result.Value.Articles,
                selectedArticleId);
        }
        finally
        {
            IsLearningOptionsLoading = false;
            NotifyLearningCollectionsChanged();
        }
    }

    public async Task<IReadOnlyList<StandaloneQuestionPickerOptionDto>?>
        LoadStandaloneQuestionPickerOptionsAsync(
            CancellationToken cancellationToken = default)
    {
        LearningOptionsError = null;

        var result =
            await queryDispatcher.SendAsync<
                GetStandaloneQuestionPickerOptionsQuery,
                IReadOnlyList<StandaloneQuestionPickerOptionDto>>(
                new GetStandaloneQuestionPickerOptionsQuery(),
                cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (result.IsFailure)
        {
            LearningOptionsError =
                result.Error
                    .FirstOrDefault()
                    ?.Message
                ?? "Не удалось загрузить самостоятельные вопросы.";

            return null;
        }

        return result.Value;
    }

    public void ApplyStandaloneQuestionSelection(
        IEnumerable<StandaloneQuestionPickerOptionDto> selectedQuestions)
    {
        ArgumentNullException.ThrowIfNull(selectedQuestions);

        if (SelectedTopic is null)
        {
            throw new InvalidOperationException(
                "Не удалось определить тему создаваемой статьи.");
        }

        AvailableQuestions.Clear();

        foreach (StandaloneQuestionPickerOptionDto question
                 in selectedQuestions
                     .OrderBy(option => option.SectionName,
                         StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(option => option.TopicName,
                         StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(option => option.Title,
                         StringComparer.CurrentCultureIgnoreCase))
        {
            AvailableQuestions.Add(
                new LearningQuestionOptionViewModel(
                    question,
                    SelectedTopic.Id,
                    SelectedTopic.Name));
        }

        NotifySelectedQuestionsChanged();
        NotifyLearningCollectionsChanged();
    }

    public void RemoveStandaloneQuestion(
        LearningQuestionOptionViewModel question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (AvailableQuestions.Remove(question))
        {
            NotifySelectedQuestionsChanged();
            NotifyLearningCollectionsChanged();
        }
    }

    public void BeginNewRelatedQuestion(
        MaterialDifficulty difficulty)
    {
        EditingRelatedQuestion = null;
        RelatedQuestionTitle = string.Empty;
        RelatedQuestionDifficulty = difficulty;
        RelatedQuestionIconKind = DefaultRelatedQuestionIconKind;
        RelatedQuestionStudyPoints = StudyPoints;
        RelatedQuestionReviewPoints = ReviewPoints;
        RelatedQuestionPromptPath = null;
        RelatedQuestionAnswerPath = null;
        IsRelatedQuestionPromptConfigured = false;
        IsRelatedQuestionAnswerConfigured = false;
        HasRelatedQuestionValidationAttempted = false;
        RelatedQuestionEditorError = null;
        IsRelatedQuestionEditorOpen = true;
    }

    public void BeginEditRelatedQuestion(
        RelatedQuestionDraftViewModel draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        EditingRelatedQuestion = draft;
        RelatedQuestionTitle = draft.Title;
        RelatedQuestionDifficulty = draft.Difficulty;
        RelatedQuestionIconKind = draft.IconKind;
        RelatedQuestionStudyPoints = draft.StudyPoints;
        RelatedQuestionReviewPoints = draft.ReviewPoints;
        RelatedQuestionPromptPath = draft.PromptPath;
        RelatedQuestionAnswerPath = draft.ReferenceAnswerPath;
        IsRelatedQuestionPromptConfigured = true;
        IsRelatedQuestionAnswerConfigured = true;
        HasRelatedQuestionValidationAttempted = false;
        RelatedQuestionEditorError = null;
        IsRelatedQuestionEditorOpen = true;
    }

    public void SetRelatedQuestionDraftFiles(
        string promptPath,
        string answerPath)
    {
        RelatedQuestionPromptPath =
            Path.GetFullPath(promptPath);
        RelatedQuestionAnswerPath =
            Path.GetFullPath(answerPath);
    }

    public void SetRelatedQuestionFileConfigured(
        bool isAnswer,
        bool isConfigured)
    {
        if (isAnswer)
        {
            IsRelatedQuestionAnswerConfigured = isConfigured;
        }
        else
        {
            IsRelatedQuestionPromptConfigured = isConfigured;
        }
    }

    public void MarkRelatedQuestionValidationAttempted()
    {
        HasRelatedQuestionValidationAttempted = true;
    }

    public void SetRelatedQuestionEditorError(
        string? message)
    {
        RelatedQuestionEditorError = message;
    }

    public void CancelRelatedQuestionEditor()
    {
        IsRelatedQuestionEditorOpen = false;
        HasRelatedQuestionValidationAttempted = false;
        RelatedQuestionEditorError = null;
        EditingRelatedQuestion = null;
    }

    public void SaveRelatedQuestionDraft()
    {
        if (string.IsNullOrWhiteSpace(
                RelatedQuestionPromptPath) ||
            string.IsNullOrWhiteSpace(
                RelatedQuestionAnswerPath))
        {
            throw new InvalidOperationException(
                "Не заданы Markdown-файлы нового вопроса.");
        }

        if (EditingRelatedQuestion is null)
        {
            NewRelatedQuestions.Add(
                new RelatedQuestionDraftViewModel(
                    Guid.NewGuid(),
                    RelatedQuestionTitle.Trim(),
                    RelatedQuestionDifficulty,
                    RelatedQuestionIconKind,
                    RelatedQuestionStudyPoints,
                    RelatedQuestionReviewPoints,
                    RelatedQuestionPromptPath,
                    RelatedQuestionAnswerPath));
        }
        else
        {
            EditingRelatedQuestion.Update(
                RelatedQuestionTitle.Trim(),
                RelatedQuestionDifficulty,
                RelatedQuestionIconKind,
                RelatedQuestionStudyPoints,
                RelatedQuestionReviewPoints,
                RelatedQuestionPromptPath,
                RelatedQuestionAnswerPath);
        }

        IsRelatedQuestionEditorOpen = false;
        HasRelatedQuestionValidationAttempted = false;
        RelatedQuestionEditorError = null;
        EditingRelatedQuestion = null;
        NotifyArticleQuestionsChanged();
    }

    public void RemoveRelatedQuestionDraft(
        RelatedQuestionDraftViewModel draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (NewRelatedQuestions.Remove(draft))
        {
            NotifyArticleQuestionsChanged();
        }
    }

    [RelayCommand]
    private void OpenIconPicker()
    {
        var currentOption =
            TopicAppearanceOptions.Icons
                .FirstOrDefault(option =>
                    option.Kind ==
                    SelectedIconKind)
            ?? TopicAppearanceOptions.Icons[0];

        var selectedIcon = dialogService
            .Show<SelectTopicIconDialogViewModel, TopicIcon?>(
                viewModel =>
                    viewModel.Initialize(
                        currentOption.Value));

        if (selectedIcon is null)
        {
            return;
        }

        var selectedOption =
            TopicAppearanceOptions.Icons
                .FirstOrDefault(option =>
                    option.Value ==
                    selectedIcon.Value);

        if (selectedOption is null)
        {
            return;
        }

        SelectedIconKind =
            selectedOption.Kind;
    }

    [RelayCommand]
    private void OpenRelatedQuestionIconPicker()
    {
        var currentOption =
            TopicAppearanceOptions.Icons
                .FirstOrDefault(option =>
                    option.Kind ==
                    RelatedQuestionIconKind)
            ?? TopicAppearanceOptions.Icons[0];

        var selectedIcon = dialogService
            .Show<SelectTopicIconDialogViewModel, TopicIcon?>(
                viewModel =>
                    viewModel.Initialize(
                        currentOption.Value));

        if (selectedIcon is null)
        {
            return;
        }

        var selectedOption =
            TopicAppearanceOptions.Icons
                .FirstOrDefault(option =>
                    option.Value ==
                    selectedIcon.Value);

        if (selectedOption is null)
        {
            return;
        }

        RelatedQuestionIconKind =
            selectedOption.Kind;
    }

    [RelayCommand]
    private void CancelCreateMaterial()
    {
        Closing?.Invoke(
            this,
            EventArgs.Empty);

        _closeRequested?.Invoke();
    }

    private void LoadArticleOptions(
        IReadOnlyList<MaterialLearningLinkOptionDto> articles,
        Guid? selectedArticleId)
    {
        AvailableArticles.Clear();

        var noneOption =
            new LearningArticleOptionViewModel(
                null,
                "Без связанной статьи");

        AvailableArticles.Add(
            noneOption);

        foreach (MaterialLearningLinkOptionDto article
                 in articles)
        {
            AvailableArticles.Add(
                new LearningArticleOptionViewModel(
                    article.Id,
                    article.Title));
        }

        SelectedArticle =
            selectedArticleId is null
                ? noneOption
                : AvailableArticles
                    .FirstOrDefault(article =>
                        article.Id ==
                        selectedArticleId)
                  ?? noneOption;
    }

    private void NotifySelectedQuestionsChanged()
    {
        OnPropertyChanged(
            nameof(SelectedQuestionCount));

        OnPropertyChanged(
            nameof(SelectedQuestionCountText));

        OnPropertyChanged(
            nameof(SelectedLinkedQuestionIds));

        NotifyArticleQuestionsChanged();
    }

    private void NotifyArticleQuestionsChanged()
    {
        OnPropertyChanged(
            nameof(ArticleQuestionCount));

        OnPropertyChanged(
            nameof(ArticleQuestionCountText));

        OnPropertyChanged(
            nameof(HasNewRelatedQuestions));

        OnPropertyChanged(
            nameof(HasArticleQuestions));
    }

    private void ResetLearningState()
    {
        IsQuestionMaterial = false;
        LearningOptionsError = null;
        IsLearningOptionsLoading = false;

        StudyPoints = 50;
        ReviewPoints = 20;

        AvailableQuestions.Clear();
        AvailableArticles.Clear();
        SelectedArticle = null;

        NewRelatedQuestions.Clear();
        IsRelatedQuestionEditorOpen = false;
        RelatedQuestionTitle = string.Empty;
        RelatedQuestionDifficulty = MaterialDifficulty.Medium;
        RelatedQuestionIconKind = DefaultRelatedQuestionIconKind;
        RelatedQuestionStudyPoints = 50;
        RelatedQuestionReviewPoints = 20;
        RelatedQuestionPromptPath = null;
        RelatedQuestionAnswerPath = null;
        IsRelatedQuestionPromptConfigured = false;
        IsRelatedQuestionAnswerConfigured = false;
        HasRelatedQuestionValidationAttempted = false;
        RelatedQuestionEditorError = null;
        EditingRelatedQuestion = null;

        NotifyLearningCollectionsChanged();
        NotifySelectedQuestionsChanged();

        OnPropertyChanged(
            nameof(SelectedArticleId));
    }

    private void ClearArticleOptions()
    {
        AvailableArticles.Clear();

        var noneOption =
            new LearningArticleOptionViewModel(
                null,
                "Без связанной статьи");

        AvailableArticles.Add(
            noneOption);

        SelectedArticle =
            noneOption;

        NotifyLearningCollectionsChanged();
    }

    private void NotifyLearningCollectionsChanged()
    {
        OnPropertyChanged(
            nameof(HasVisibleQuestions));

        OnPropertyChanged(
            nameof(HasAvailableArticles));

        OnPropertyChanged(
            nameof(CanProceedFromLinks));

        OnPropertyChanged(
            nameof(CanProceedFromExperience));
    }

    private static string FormatDifficulty(
        string difficulty) =>
        difficulty switch
        {
            "Easy" => "Лёгкий",
            "Medium" => "Средний",
            "Hard" => "Сложный",
            _ => difficulty,
        };

    private static int GetReviewMaximum(int studyPoints)
    {
        int maximumBelowStudy =
            ((studyPoints - 1) / ExperiencePointsStep) * ExperiencePointsStep;

        return Math.Max(
            MaterialExperienceRewards.MinPoints,
            maximumBelowStudy);
    }

    private static string FormatCount(
        int count,
        string one,
        string few,
        string many)
    {
        int lastTwoDigits =
            count % 100;

        if (lastTwoDigits is
            >= 11 and <= 14)
        {
            return $"{count} {many}";
        }

        return (count % 10) switch
        {
            1 => $"{count} {one}",
            2 or 3 or 4 =>
                $"{count} {few}",
            _ => $"{count} {many}",
        };
    }
}

public sealed class LearningQuestionOptionViewModel
{
    public LearningQuestionOptionViewModel(
        StandaloneQuestionPickerOptionDto option,
        Guid targetTopicId,
        string targetTopicName)
    {
        ArgumentNullException.ThrowIfNull(option);

        Id = option.Id;
        Title = option.Title;
        Difficulty = FormatDifficulty(option.Difficulty);
        StudyPoints = option.StudyPoints;
        ReviewPoints = option.ReviewPoints;
        TopicId = option.TopicId;
        TopicName = option.TopicName;
        SectionId = option.SectionId;
        SectionName = option.SectionName;
        TargetTopicId = targetTopicId;
        TargetTopicName = targetTopicName;
    }

    public Guid Id { get; }

    public string Title { get; }

    public string Difficulty { get; }

    public int StudyPoints { get; }

    public int ReviewPoints { get; }

    public Guid TopicId { get; }

    public string TopicName { get; }

    public Guid SectionId { get; }

    public string SectionName { get; }

    public Guid TargetTopicId { get; }

    public string TargetTopicName { get; }

    public bool MovesToArticleTopic =>
        TopicId != TargetTopicId;

    public string OriginText =>
        $"{SectionName} → {TopicName}";

    public string MoveText =>
        MovesToArticleTopic
            ? $"Будет перенесён в тему «{TargetTopicName}»"
            : "Уже находится в теме статьи";

    public string ExperienceText =>
        $"{StudyPoints} / {ReviewPoints} XP";

    private static string FormatDifficulty(string difficulty) =>
        difficulty switch
        {
            "Easy" => "Лёгкий",
            "Medium" => "Средний",
            "Hard" => "Сложный",
            _ => difficulty,
        };
}

public sealed partial class RelatedQuestionDraftViewModel(
    Guid draftId,
    string title,
    MaterialDifficulty difficulty,
    PackIconKind iconKind,
    int studyPoints,
    int reviewPoints,
    string promptPath,
    string referenceAnswerPath)
    : ObservableObject
{
    public Guid DraftId { get; } = draftId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DifficultyText))]
    private MaterialDifficulty _difficulty = difficulty;

    [ObservableProperty]
    private string _title = title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconKey))]
    private PackIconKind _iconKind = iconKind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExperienceText))]
    private int _studyPoints = studyPoints;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExperienceText))]
    private int _reviewPoints = reviewPoints;

    [ObservableProperty]
    private string _promptPath = promptPath;

    [ObservableProperty]
    private string _referenceAnswerPath = referenceAnswerPath;

    public string IconKey =>
        IconKind.ToString();

    public string DifficultyText =>
        Difficulty switch
        {
            MaterialDifficulty.Easy => "Лёгкий",
            MaterialDifficulty.Medium => "Средний",
            MaterialDifficulty.Hard => "Сложный",
            _ => Difficulty.ToString(),
        };

    public string ExperienceText =>
        $"{StudyPoints} / {ReviewPoints} XP";

    public void Update(
        string title,
        MaterialDifficulty difficulty,
        PackIconKind iconKind,
        int studyPoints,
        int reviewPoints,
        string promptPath,
        string referenceAnswerPath)
    {
        Title = title;
        Difficulty = difficulty;
        IconKind = iconKind;
        StudyPoints = studyPoints;
        ReviewPoints = reviewPoints;
        PromptPath = promptPath;
        ReferenceAnswerPath = referenceAnswerPath;
    }
}

public sealed record RelatedQuestionDifficultyOption(
    string Name,
    MaterialDifficulty Value);

public sealed record LearningArticleOptionViewModel(
    Guid? Id,
    string Title);
