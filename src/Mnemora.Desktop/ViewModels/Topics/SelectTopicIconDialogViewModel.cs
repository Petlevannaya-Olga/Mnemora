using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Domain.Topics;

namespace Mnemora.Desktop.ViewModels.Topics;

public sealed partial class SelectTopicIconDialogViewModel
    : ViewModelBase,
      IDialogViewModel<TopicIcon?>
{
    private readonly ListCollectionView _iconsView;

    private string _searchText = string.Empty;
    private string _selectedCategory = "Все";
    private TopicIconOption? _selectedIcon;

    public SelectTopicIconDialogViewModel()
    {
        _iconsView = new ListCollectionView(
            TopicAppearanceOptions.Icons.ToList());

        _iconsView.Filter = FilterIcon;

        Categories =
        [
            "Все",
            .. TopicAppearanceOptions.Icons
                .Select(icon => icon.Category)
                .Distinct()
                .OrderBy(category => category)
        ];
    }

    public event EventHandler<
        DialogCloseRequestedEventArgs<TopicIcon?>>?
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

    public TopicIconOption? SelectedIcon
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
        TopicIcon selectedIcon)
    {
        SearchText = string.Empty;
        SelectedCategory = "Все";

        SelectedIcon =
            TopicAppearanceOptions.Icons
                .FirstOrDefault(icon =>
                    icon.Value == selectedIcon)
            ?? TopicAppearanceOptions.Icons[0];
    }

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void Select()
    {
        CloseRequested?.Invoke(
            this,
            new DialogCloseRequestedEventArgs<TopicIcon?>(
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
            new DialogCloseRequestedEventArgs<TopicIcon?>(
                null,
                false));
    }

    public void CancelPendingOperation()
    {
    }

    private bool FilterIcon(
        object item)
    {
        if (item is not TopicIconOption icon)
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