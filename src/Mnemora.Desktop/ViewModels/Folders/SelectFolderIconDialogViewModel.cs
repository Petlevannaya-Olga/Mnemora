using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Domain.LibraryContainers;

namespace Mnemora.Desktop.ViewModels.Folders;

public sealed partial class SelectFolderIconDialogViewModel
    : ViewModelBase,
      IDialogViewModel<FolderIcon?>
{
    private readonly ListCollectionView _iconsView;

    private string _searchText = string.Empty;
    private string _selectedCategory = "Все";
    private FolderIconOption? _selectedIcon;

    public SelectFolderIconDialogViewModel()
    {
        _iconsView = new ListCollectionView(
            FolderAppearanceOptions.Icons.ToList());

        _iconsView.Filter = FilterIcon;

        Categories =
        [
            "Все",
            .. FolderAppearanceOptions.Icons
                .Select(icon => icon.Category)
                .Distinct()
                .OrderBy(category => category)
        ];
    }

    public event EventHandler<
        DialogCloseRequestedEventArgs<FolderIcon?>>?
        CloseRequested;

    public ICollectionView IconsView =>
        _iconsView;

    public IReadOnlyList<string> Categories { get; }

    public bool IsBusy => false;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            _iconsView.Refresh();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(
                    ref _selectedCategory,
                    value))
            {
                return;
            }

            _iconsView.Refresh();
        }
    }

    public FolderIconOption? SelectedIcon
    {
        get => _selectedIcon;
        set
        {
            if (!SetProperty(
                    ref _selectedIcon,
                    value))
            {
                return;
            }

            SelectCommand.NotifyCanExecuteChanged();
        }
    }

    public void Initialize(
        FolderIcon selectedIcon)
    {
        SearchText = string.Empty;
        SelectedCategory = "Все";

        SelectedIcon =
            FolderAppearanceOptions.Icons
                .FirstOrDefault(icon =>
                    icon.Value == selectedIcon)
            ?? FolderAppearanceOptions.Icons[0];
    }

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void Select()
    {
        CloseRequested?.Invoke(
            this,
            new DialogCloseRequestedEventArgs<FolderIcon?>(
                SelectedIcon!.Value,
                true));
    }

    private bool CanSelect()
    {
        return SelectedIcon is not null;
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(
            this,
            new DialogCloseRequestedEventArgs<FolderIcon?>(
                null,
                false));
    }

    public void CancelPendingOperation()
    {
    }

    private bool FilterIcon(
        object item)
    {
        if (item is not FolderIconOption icon)
        {
            return false;
        }

        var categoryMatches =
            SelectedCategory == "Все"
            || icon.Category == SelectedCategory;

        if (!categoryMatches)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var searchText = SearchText.Trim();

        return icon.Name.Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase)
               || icon.Value.ToString().Contains(
                   searchText,
                   StringComparison.OrdinalIgnoreCase);
    }
}