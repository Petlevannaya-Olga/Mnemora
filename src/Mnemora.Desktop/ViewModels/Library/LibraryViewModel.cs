using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Library.Get;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.ViewModels.Topics;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryViewModel(
    IQueryDispatcher queryDispatcher,
    IDialogService dialogService)
    : ObservableObject
{
    public ObservableCollection<LibrarySectionDto> Sections { get; } = [];

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
            var query = new GetLibraryQuery();

            var result = await queryDispatcher.SendAsync<
                GetLibraryQuery,
                IReadOnlyList<LibrarySectionDto>>(
                query,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                    ?? "Не удалось загрузить библиотеку";

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
    private async Task AddSectionAsync(
        CancellationToken cancellationToken)
    {
        var sectionId = dialogService.Show<
            CreateSectionDialogViewModel,
            Guid?>();

        if (sectionId is null)
        {
            return;
        }

        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task AddTopicAsync(
        LibrarySectionDto? section,
        CancellationToken cancellationToken)
    {
        if (section is null)
        {
            return;
        }

        var topicId = dialogService.Show<
            CreateTopicDialogViewModel,
            Guid?>(
            viewModel => viewModel.Initialize(
                section.Id,
                section.Name));

        if (topicId is null)
        {
            return;
        }

        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private Task ReloadAsync(
        CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSections));
        OnPropertyChanged(nameof(IsEmpty));
    }
}