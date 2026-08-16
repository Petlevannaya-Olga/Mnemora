using System.Collections.ObjectModel;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibrarySectionRowViewModel
{
    private readonly int _capacity;

    public LibrarySectionRowViewModel(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public ObservableCollection<LibrarySectionCardViewModel> Sections { get; } = [];

    public bool IsFull => Sections.Count >= _capacity;

    public void Add(LibrarySectionCardViewModel section)
    {
        ArgumentNullException.ThrowIfNull(section);

        if (IsFull)
        {
            throw new InvalidOperationException("Строка разделов уже заполнена.");
        }

        Sections.Add(section);
    }
}