using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Domain.Sections;

namespace Mnemora.Desktop.ViewModels.Sections;

public sealed partial class SelectSectionIconDialogViewModel
    : ViewModelBase,
      IDialogViewModel<SectionIcon?>
{
    private readonly ListCollectionView _iconsView;
    private string _searchText = string.Empty;
    private string _selectedCategory = "Все";
    private SectionIconOption? _selectedIcon;

    public SelectSectionIconDialogViewModel()
    {
        _iconsView = new ListCollectionView(
            SectionAppearanceOptions.Icons.ToList());

        _iconsView.Filter = FilterIcon;

        Categories =
        [
            "Все",
            .. SectionAppearanceOptions.Icons
                .Select(icon => icon.Category)
                .Distinct()
                .OrderBy(category => category)
        ];
    }

    public event EventHandler<DialogCloseRequestedEventArgs<SectionIcon?>>?
        CloseRequested;

    public ICollectionView IconsView => _iconsView;

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
            if (!SetProperty(ref _selectedCategory, value))
            {
                return;
            }

            _iconsView.Refresh();
        }
    }

    public SectionIconOption? SelectedIcon
    {
        get => _selectedIcon;
        set
        {
            if (!SetProperty(ref _selectedIcon, value))
            {
                return;
            }

            SelectCommand.NotifyCanExecuteChanged();
        }
    }

    public void Initialize(SectionIcon selectedIcon)
    {
        SelectedIcon = SectionAppearanceOptions.Icons
            .FirstOrDefault(icon => icon.Value == selectedIcon);
    }

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void Select()
    {
        CloseRequested?.Invoke(
            this,
            new DialogCloseRequestedEventArgs<SectionIcon?>(
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
            new DialogCloseRequestedEventArgs<SectionIcon?>(
                null,
                false));
    }

    public void CancelPendingOperation()
    {
    }

    private bool FilterIcon(object item)
    {
        if (item is not SectionIconOption icon)
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

        return icon.Name.Contains(
                   SearchText.Trim(),
                   StringComparison.OrdinalIgnoreCase)
               || icon.Value.ToString().Contains(
                   SearchText.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }
}