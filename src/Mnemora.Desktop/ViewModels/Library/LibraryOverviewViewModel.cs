using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.Get;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryOverviewViewModel : ViewModelBase
{
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly ILogger<LibraryOverviewViewModel> _logger;
    private readonly List<LibrarySectionCardViewModel> _allSections = [];

    public LibraryOverviewViewModel(
        IQueryDispatcher queryDispatcher,
        ILogger<LibraryOverviewViewModel> logger)
    {
        _queryDispatcher = queryDispatcher;
        _logger = logger;

        SortOptions =
        [
            new("Мой порядок", LibrarySectionSortMode.Custom),
            new("По названию", LibrarySectionSortMode.Name),
            new("Сначала новые", LibrarySectionSortMode.Newest),
            new("По прогрессу", LibrarySectionSortMode.Progress)
        ];

        _selectedSortOption = SortOptions[0];
    }

    public ObservableCollection<LibrarySectionCardViewModel> Sections { get; } = [];

    public IReadOnlyList<LibrarySectionSortOption> SortOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private LibrarySectionSortOption _selectedSortOption;

    public bool HasSections => Sections.Count > 0;

    public bool HasSourceSections => _allSections.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !IsLoading && !HasError && !HasSourceSections;

    public bool HasNoSearchResults =>
        !IsLoading &&
        !HasError &&
        HasSourceSections &&
        !HasSections;

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
            var result = await _queryDispatcher.SendAsync<
                GetLibraryQuery,
                IReadOnlyList<LibrarySectionDto>>(
                new GetLibraryQuery(),
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

            _allSections.Clear();
            _allSections.AddRange(result.Value.Select(section => new LibrarySectionCardViewModel(section)));

            ApplyFilterAndSort();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ignore
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить обзор библиотеки");
            ErrorMessage = "Не удалось загрузить библиотеку";
        }
        finally
        {
            IsLoading = false;
            NotifyCollectionStateChanged();
        }
    }

    [RelayCommand]
    private Task ReloadAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }

    partial void OnSearchTextChanged(string? value)
    {
        ApplyFilterAndSort();
    }

    partial void OnSelectedSortOptionChanged(LibrarySectionSortOption value)
    {
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<LibrarySectionCardViewModel> sections = _allSections;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string searchText = SearchText.Trim();

            sections = sections.Where(section =>
                section.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        sections = SelectedSortOption.Mode switch
        {
            LibrarySectionSortMode.Custom => sections
                .OrderBy(section => section.SortOrder ?? int.MaxValue)
                .ThenBy(section => section.CreatedAt),

            LibrarySectionSortMode.Name => sections
                .OrderBy(section => section.Name, StringComparer.OrdinalIgnoreCase),

            LibrarySectionSortMode.Newest => sections
                .OrderByDescending(section => section.CreatedAt),

            LibrarySectionSortMode.Progress => sections
                .OrderByDescending(section => section.ProgressPercentage ?? -1)
                .ThenBy(section => section.Name, StringComparer.OrdinalIgnoreCase),

            _ => sections
        };

        Sections.Clear();

        foreach (var section in sections)
        {
            Sections.Add(section);
        }

        NotifyCollectionStateChanged();
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSections));
        OnPropertyChanged(nameof(HasSourceSections));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}

public enum LibrarySectionSortMode
{
    Custom,
    Name,
    Newest,
    Progress
}

public sealed record LibrarySectionSortOption(
    string Name,
    LibrarySectionSortMode Mode);