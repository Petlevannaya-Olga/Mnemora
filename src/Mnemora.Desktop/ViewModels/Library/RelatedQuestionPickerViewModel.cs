using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mnemora.Contracts;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class RelatedQuestionPickerViewModel : ObservableObject
{
    private readonly List<StandaloneQuestionPickerItemViewModel> _allQuestions;
    private readonly Guid _targetContainerId;
    private readonly string _targetContainerName;

    [ObservableProperty]
    private string? _searchText;

    public RelatedQuestionPickerViewModel(
        IReadOnlyList<StandaloneQuestionPickerOptionDto> options,
        IReadOnlyCollection<Guid> selectedQuestionIds,
        Guid targetContainerId,
        string targetContainerName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selectedQuestionIds);

        _targetContainerId = targetContainerId;
        _targetContainerName = targetContainerName;

        HashSet<Guid> selected = selectedQuestionIds.ToHashSet();

        _allQuestions = options
            .Select(option =>
                new StandaloneQuestionPickerItemViewModel(
                    option,
                    targetContainerId,
                    OnSelectionChanged))
            .ToList();

        // Поле _allQuestions уже инициализировано, поэтому callback изменения
        // выбора безопасно может пересчитать агрегаты.
        foreach (StandaloneQuestionPickerItemViewModel question in _allQuestions)
        {
            question.IsSelected = selected.Contains(question.Id);
        }

        BuildNavigation(options);

        // Чаще всего нужные вопросы находятся рядом со статьёй. Если в выбранном месте
        // нет самостоятельных вопросов, сразу показываем всю библиотеку.
        var targetContainer = Sections
            .SelectMany(section => section.Containers)
            .FirstOrDefault(container => container.Id == targetContainerId);

        if (targetContainer is not null &&
            _allQuestions.Any(question => question.ContainerId == targetContainerId))
        {
            SelectContainer(targetContainerId);
        }
        else
        {
            SelectAll();
        }

        NotifySelectionState();
    }

    public ObservableCollection<StandaloneQuestionPickerSectionViewModel>
        Sections { get; } = [];

    public ObservableCollection<StandaloneQuestionPickerItemViewModel>
        VisibleQuestions { get; } = [];

    public bool IsAllSelected =>
        Sections.All(section =>
            !section.IsSelected &&
            section.Containers.All(container => !container.IsSelected));

    public bool HasQuestions =>
        VisibleQuestions.Count > 0;

    public int VisibleQuestionCount =>
        VisibleQuestions.Count;

    public string VisibleQuestionCountText =>
        FormatCount(
            VisibleQuestionCount,
            "вопрос",
            "вопроса",
            "вопросов");

    public int SelectedCount =>
        _allQuestions.Count(question => question.IsSelected);

    public string SelectedCountText =>
        SelectedCount == 0
            ? "Ничего не выбрано"
            : $"Выбрано: {FormatCount(SelectedCount, "вопрос", "вопроса", "вопросов")}";

    public int CrossContainerSelectedCount =>
        _allQuestions.Count(question =>
            question.IsSelected &&
            question.ContainerId != _targetContainerId);

    public bool HasCrossContainerSelection =>
        CrossContainerSelectedCount > 0;

    public string MoveWarningText =>
        CrossContainerSelectedCount == 0
            ? string.Empty
            : $"В других местах выбрано: {CrossContainerSelectedCount}. " +
              $"После создания статьи эти вопросы будут перенесены в «{_targetContainerName}».";

    public IReadOnlyList<StandaloneQuestionPickerOptionDto>
        GetSelectedOptions() =>
        _allQuestions
            .Where(question => question.IsSelected)
            .Select(question => question.Option)
            .ToArray();

    partial void OnSearchTextChanged(string? value)
    {
        ApplyFilter();
    }

    public void SelectAll()
    {
        foreach (StandaloneQuestionPickerSectionViewModel section in Sections)
        {
            section.IsSelected = false;

            foreach (StandaloneQuestionPickerContainerViewModel container in section.Containers)
            {
                container.IsSelected = false;
            }
        }

        OnPropertyChanged(nameof(IsAllSelected));
        ApplyFilter();
    }

    public void SelectSection(Guid sectionId)
    {
        foreach (StandaloneQuestionPickerSectionViewModel section in Sections)
        {
            section.IsSelected = section.Id == sectionId;

            foreach (StandaloneQuestionPickerContainerViewModel container in section.Containers)
            {
                container.IsSelected = false;
            }
        }

        OnPropertyChanged(nameof(IsAllSelected));
        ApplyFilter();
    }

    public void SelectContainer(Guid containerId)
    {
        foreach (StandaloneQuestionPickerSectionViewModel section in Sections)
        {
            section.IsSelected = false;

            foreach (StandaloneQuestionPickerContainerViewModel container in section.Containers)
            {
                container.IsSelected = container.Id == containerId;
            }
        }

        OnPropertyChanged(nameof(IsAllSelected));
        ApplyFilter();
    }

    private void BuildNavigation(
        IReadOnlyList<StandaloneQuestionPickerOptionDto> options)
    {
        foreach (var sectionGroup in options
                     .GroupBy(option => new { option.SectionId, option.SectionName })
                     .OrderBy(group => group.Key.SectionName, StringComparer.CurrentCultureIgnoreCase))
        {
            var section = new StandaloneQuestionPickerSectionViewModel(
                sectionGroup.Key.SectionId,
                sectionGroup.Key.SectionName);

            foreach (var containerGroup in sectionGroup
                         .GroupBy(option => new { option.ContainerId, option.ContainerName })
                         .OrderBy(group => group.Key.ContainerName, StringComparer.CurrentCultureIgnoreCase))
            {
                section.Containers.Add(
                    new StandaloneQuestionPickerContainerViewModel(
                        containerGroup.Key.ContainerId,
                        containerGroup.Key.ContainerName,
                        containerGroup.Count()));
            }

            Sections.Add(section);
        }
    }

    private void ApplyFilter()
    {
        string search = SearchText?.Trim() ?? string.Empty;

        Guid? selectedSectionId = Sections
            .FirstOrDefault(section => section.IsSelected)
            ?.Id;

        Guid? selectedContainerId = Sections
            .SelectMany(section => section.Containers)
            .FirstOrDefault(container => container.IsSelected)
            ?.Id;

        IEnumerable<StandaloneQuestionPickerItemViewModel> query = _allQuestions;

        // Поиск глобальный: когда пользователь вводит текст, текущий раздел/папка
        // не ограничивают результаты.
        if (search.Length > 0)
        {
            query = query.Where(question =>
                question.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                question.ContainerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                question.SectionName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        else if (selectedContainerId is not null)
        {
            query = query.Where(question => question.ContainerId == selectedContainerId.Value);
        }
        else if (selectedSectionId is not null)
        {
            query = query.Where(question => question.SectionId == selectedSectionId.Value);
        }

        VisibleQuestions.Clear();

        foreach (StandaloneQuestionPickerItemViewModel question in query)
        {
            VisibleQuestions.Add(question);
        }

        OnPropertyChanged(nameof(HasQuestions));
        OnPropertyChanged(nameof(VisibleQuestionCount));
        OnPropertyChanged(nameof(VisibleQuestionCountText));
    }

    private void OnSelectionChanged()
    {
        NotifySelectionState();
    }

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(CrossContainerSelectedCount));
        OnPropertyChanged(nameof(HasCrossContainerSelection));
        OnPropertyChanged(nameof(MoveWarningText));
    }

    private static string FormatCount(
        int count,
        string one,
        string few,
        string many)
    {
        int lastTwoDigits = count % 100;

        if (lastTwoDigits is >= 11 and <= 14)
        {
            return $"{count} {many}";
        }

        return (count % 10) switch
        {
            1 => $"{count} {one}",
            2 or 3 or 4 => $"{count} {few}",
            _ => $"{count} {many}",
        };
    }
}

public sealed partial class StandaloneQuestionPickerItemViewModel(
    StandaloneQuestionPickerOptionDto option,
    Guid targetContainerId,
    Action selectionChanged)
    : ObservableObject
{
    public StandaloneQuestionPickerOptionDto Option { get; } = option;

    public Guid Id => Option.Id;

    public string Title => Option.Title;

    public Guid ContainerId => Option.ContainerId;

    public string ContainerName => Option.ContainerName;

    public Guid SectionId => Option.SectionId;

    public string SectionName => Option.SectionName;

    public string PathText =>
        $"{SectionName} → {ContainerName}";

    public string DifficultyText =>
        Option.Difficulty switch
        {
            "Easy" => "Лёгкий",
            "Medium" => "Средний",
            "Hard" => "Сложный",
            _ => Option.Difficulty,
        };

    public string ExperienceText =>
        $"{Option.StudyPoints} / {Option.ReviewPoints} XP";

    public bool MovesToArticleContainer =>
        ContainerId != targetContainerId;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        selectionChanged();
    }
}

public sealed partial class StandaloneQuestionPickerSectionViewModel(
    Guid id,
    string name)
    : ObservableObject
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    public ObservableCollection<StandaloneQuestionPickerContainerViewModel>
        Containers { get; } = [];

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class StandaloneQuestionPickerContainerViewModel(
    Guid id,
    string name,
    int questionCount)
    : ObservableObject
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    public int QuestionCount { get; } = questionCount;

    public string CountText =>
        QuestionCount.ToString();

    [ObservableProperty]
    private bool _isSelected;
}
