using System.Collections.ObjectModel;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibraryManagementSectionViewModel
{
    public LibraryManagementSectionViewModel(LibrarySectionOverviewDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
    }

    public LibrarySectionOverviewDto Source { get; }

    public Guid Id => Source.Id;
    public string Name => Source.Name;
    public string Color => Source.Color;
    public string Icon => Source.Icon;
    public DateTime CreatedAt => Source.CreatedAt;
    public DateTime UpdatedAt => Source.UpdatedAt;
    public DateTime LastActivityAt => Source.LastActivityAt;
    public int FoldersCount => Source.FoldersCount;
    public int MaterialsCount => Source.MaterialsCount;
    public int ArticlesCount => Source.ArticlesCount;
    public int QuestionsCount => Source.QuestionsCount;

    public DateTime CreatedAtLocal => ToLocalTime(CreatedAt);
    public DateTime UpdatedAtLocal => ToLocalTime(UpdatedAt);
    public DateTime LastActivityAtLocal => ToLocalTime(LastActivityAt);

    public string FoldersSummaryText =>
        FormatCount(FoldersCount, "папка", "папки", "папок");

    public string MaterialsSummaryText =>
        FormatCount(MaterialsCount, "материал", "материала", "материалов");

    public string StructureText =>
        $"{FormatCount(FoldersCount, "папка", "папки", "папок")} • " +
        FormatCount(MaterialsCount, "материал", "материала", "материалов");

    private static DateTime ToLocalTime(DateTime value)
    {
        DateTime utcValue = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return utcValue.ToLocalTime();
    }

    private static string FormatCount(int count, string one, string few, string many)
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

public sealed class LibraryManagementSectionRowViewModel
{
    private readonly int _capacity;

    public LibraryManagementSectionRowViewModel(int capacity, bool isFirstRow = false)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        IsFirstRow = isFirstRow;
    }

    public ObservableCollection<LibraryManagementSectionViewModel> Sections { get; } = [];

    public bool IsFirstRow { get; }

    public bool IsFull => Sections.Count >= _capacity;

    public void Add(LibraryManagementSectionViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);

        if (IsFull)
        {
            throw new InvalidOperationException("Строка разделов уже заполнена.");
        }

        Sections.Add(section);
    }
}

public sealed record LibraryManagementSectionSortOption(
    string Name,
    Mnemora.Application.Library.GetManagementSectionsPage.LibraryManagementSectionSort Sort);
