using System.Collections.ObjectModel;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibrarySectionRowViewModel
{
    private const int MaximumSections = 3;

    public ObservableCollection<LibrarySectionCardViewModel> Sections { get; } = [];

    public bool IsFull => Sections.Count >= MaximumSections;

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