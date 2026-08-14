using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Queries;
using Mnemora.Application.Sections.GetAll;
using Mnemora.Contracts;
using Mnemora.Desktop.Dialogs;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryViewModel(
    IQueryDispatcher queryDispatcher,
    ICreateSectionDialogService createSectionDialogService)
    : ObservableObject
{
    public ObservableCollection<SectionListItemDto> Sections { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? _errorMessage;

    public bool HasSections => Sections.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !IsLoading && !HasError && !HasSections;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var query = new GetSectionsQuery();

            var result = await queryDispatcher.SendAsync<
                GetSectionsQuery,
                IReadOnlyList<SectionListItemDto>>(
                query,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                    ?? "Не удалось загрузить разделы";

                return;
            }

            Sections.Clear();

            foreach (var section in result.Value)
            {
                Sections.Add(section);
            }

            NotifyCollectionStateChanged();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddSectionAsync()
    {
        var sectionId = createSectionDialogService.ShowDialog();

        if (sectionId is null)
        {
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private Task ReloadAsync()
    {
        return LoadAsync();
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSections));
        OnPropertyChanged(nameof(IsEmpty));
    }
}