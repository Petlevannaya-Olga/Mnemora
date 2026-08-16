using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.Get;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.ViewModels.Topics;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryManagementViewModel(
    IQueryDispatcher queryDispatcher,
    IDialogService dialogService,
    ISettingsService settingsService,
    ILogger<LibraryManagementViewModel> logger)
    : ViewModelBase
{
    private bool _isViewModeLoaded;
    private Task? _loadTask;
    private bool _reloadRequested;

    public ObservableCollection<LibrarySectionDto>
        Sections { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))] [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? _errorMessage;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsTableView))] [NotifyPropertyChangedFor(nameof(IsTilesView))]
    private LibraryViewMode _viewMode =
        LibraryViewMode.Table;

    public bool HasSections =>
        Sections.Count > 0;

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    public bool IsEmpty =>
        !IsLoading &&
        !HasError &&
        !HasSections;

    public bool IsTableView =>
        ViewMode ==
        LibraryViewMode.Table;

    public bool IsTilesView =>
        ViewMode ==
        LibraryViewMode.Tiles;

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_loadTask is { IsCompleted: false })
        {
            _reloadRequested = true;
            return _loadTask;
        }

        _loadTask = LoadUntilCurrentAsync(cancellationToken);
        return _loadTask;
    }

    private async Task LoadUntilCurrentAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            do
            {
                _reloadRequested = false;
                await LoadCoreAsync(cancellationToken);
            }
            while (_reloadRequested && !cancellationToken.IsCancellationRequested);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;

        await EnsureViewModeLoadedAsync(cancellationToken);

        var result = await queryDispatcher.SendAsync<GetLibraryQuery, IReadOnlyList<LibrarySectionDto>>(
            new GetLibraryQuery(),
            cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (result.IsFailure)
        {
            ErrorMessage = result.Error.FirstOrDefault()?.Message ?? "Не удалось загрузить библиотеку";
            return;
        }

        Sections.Clear();

        foreach (var section in result.Value)
        {
            Sections.Add(section);
        }

        NotifyCollectionStateChanged();
    }

    [RelayCommand]
    private async Task AddSectionAsync(CancellationToken cancellationToken)
    {
        var sectionId = dialogService.Show<CreateSectionDialogViewModel, Guid?>();

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

        var topicId =
            dialogService.Show<
                CreateTopicDialogViewModel,
                Guid?>(viewModel =>
                viewModel.Initialize(
                    section.Id,
                    section.Name));

        if (topicId is null)
        {
            return;
        }

        await LoadAsync(
            cancellationToken);
    }

    [RelayCommand]
    private async Task EditTopicAsync(
        LibraryTopicDto? topic,
        CancellationToken cancellationToken)
    {
        if (topic is null)
        {
            return;
        }

        var topicId =
            dialogService.Show<
                EditTopicDialogViewModel,
                Guid?>(viewModel =>
                viewModel.Initialize(
                    topic));

        if (topicId is null)
        {
            return;
        }

        await LoadAsync(
            cancellationToken);
    }
    
    [RelayCommand]
    private async Task DeleteTopicAsync(
        LibraryTopicDto? topic,
        CancellationToken cancellationToken)
    {
        if (topic is null)
        {
            return;
        }

        var wasDeleted =
            dialogService.Show<
                DeleteTopicDialogViewModel,
                bool>(
                viewModel =>
                    viewModel.Initialize(
                        topic));

        if (!wasDeleted)
        {
            return;
        }

        await LoadAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task EditSectionAsync(
        LibrarySectionDto? section,
        CancellationToken cancellationToken)
    {
        if (section is null)
        {
            return;
        }

        var sectionId =
            dialogService.Show<
                EditSectionDialogViewModel,
                Guid?>(viewModel =>
                viewModel.Initialize(
                    section));

        if (sectionId is null)
        {
            return;
        }

        await LoadAsync(
            cancellationToken);
    }

    [RelayCommand]
    private async Task DeleteSectionAsync(
        LibrarySectionDto? section,
        CancellationToken cancellationToken)
    {
        if (section is null)
        {
            return;
        }

        bool wasDeleted =
            dialogService.Show<
                DeleteSectionDialogViewModel,
                bool>(viewModel =>
                viewModel.Initialize(
                    section));

        if (!wasDeleted)
        {
            return;
        }

        await LoadAsync(
            cancellationToken);
    }

    [RelayCommand]
    private Task ShowTableViewAsync(
        CancellationToken cancellationToken)
    {
        return SetViewModeAsync(
            LibraryViewMode.Table,
            cancellationToken);
    }

    [RelayCommand]
    private Task ShowTilesViewAsync(
        CancellationToken cancellationToken)
    {
        return SetViewModeAsync(
            LibraryViewMode.Tiles,
            cancellationToken);
    }

    [RelayCommand]
    private Task ReloadAsync(
        CancellationToken cancellationToken)
    {
        return LoadAsync(
            cancellationToken);
    }

    private async Task EnsureViewModeLoadedAsync(
        CancellationToken cancellationToken)
    {
        if (_isViewModeLoaded)
        {
            return;
        }

        try
        {
            var settings =
                await settingsService.LoadAsync(
                    cancellationToken);

            ViewMode =
                settings.LibraryViewMode;

            _isViewModeLoaded =
                true;
        }
        catch (OperationCanceledException)
            when (cancellationToken
                      .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Не удалось загрузить режим " +
                "просмотра библиотеки");

            ViewMode =
                LibraryViewMode.Table;

            _isViewModeLoaded =
                true;
        }
    }

    private async Task SetViewModeAsync(
        LibraryViewMode viewMode,
        CancellationToken cancellationToken)
    {
        if (ViewMode == viewMode)
        {
            return;
        }

        ViewMode = viewMode;

        try
        {
            await settingsService
                .SaveLibraryViewModeAsync(
                    viewMode,
                    cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                      .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Не удалось сохранить режим " +
                "просмотра библиотеки {ViewMode}",
                viewMode);
        }
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(
            nameof(HasSections));

        OnPropertyChanged(
            nameof(IsEmpty));
    }
}