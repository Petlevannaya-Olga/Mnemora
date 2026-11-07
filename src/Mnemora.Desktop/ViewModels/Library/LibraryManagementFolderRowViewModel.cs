using System.Collections.ObjectModel;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibraryManagementFolderRowViewModel
{
    private readonly int _capacity;

    public LibraryManagementFolderRowViewModel(int capacity, bool isFirstRow = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        IsFirstRow = isFirstRow;
    }

    public ObservableCollection<LibraryManagementOrderItemViewModel> Folders { get; } = [];

    public bool IsFirstRow { get; }

    public bool IsFull => Folders.Count >= _capacity;

    public void Add(LibraryManagementOrderItemViewModel folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (IsFull)
        {
            throw new InvalidOperationException("Строка папок уже заполнена.");
        }

        Folders.Add(folder);
    }
}
