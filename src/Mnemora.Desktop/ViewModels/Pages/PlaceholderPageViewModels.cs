using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Pages;

public abstract class PlaceholderPageViewModel(string title)
    : ViewModelBase
{
    public string Title { get; } = title;
}

public sealed class PracticeViewModel()
    : PlaceholderPageViewModel("Практика");

public sealed class TrainingViewModel()
    : PlaceholderPageViewModel("Тренировка");

public sealed class PlanViewModel()
    : PlaceholderPageViewModel("Мой план");

public sealed class ProgressViewModel()
    : PlaceholderPageViewModel("Прогресс");

public sealed class SettingsViewModel()
    : PlaceholderPageViewModel("Настройки");