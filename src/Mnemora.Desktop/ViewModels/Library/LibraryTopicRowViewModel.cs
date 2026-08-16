using System.Collections.ObjectModel;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibraryTopicRowViewModel
{
    private readonly int _capacity;

    public LibraryTopicRowViewModel(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public ObservableCollection<LibraryTopicCardViewModel> Topics { get; } = [];

    public bool IsFull => Topics.Count >= _capacity;

    public void Add(LibraryTopicCardViewModel topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        if (IsFull)
        {
            throw new InvalidOperationException("Строка тем уже заполнена.");
        }

        Topics.Add(topic);
    }
}