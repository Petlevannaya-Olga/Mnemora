using System.Collections.ObjectModel;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibraryManagementTopicRowViewModel
{
    private readonly int _capacity;

    public LibraryManagementTopicRowViewModel(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public ObservableCollection<LibraryManagementOrderItemViewModel> Topics { get; } = [];

    public bool IsFull => Topics.Count >= _capacity;

    public void Add(LibraryManagementOrderItemViewModel topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        if (IsFull)
        {
            throw new InvalidOperationException("Строка тем уже заполнена.");
        }

        Topics.Add(topic);
    }
}
