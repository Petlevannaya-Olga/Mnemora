using CommunityToolkit.Mvvm.Input;
using Mnemora.Shared;
using Mnemora.Application.Commands;
using Mnemora.Application.LibraryContainers.Update;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Domain.LibraryContainers;

namespace Mnemora.Desktop.ViewModels.Folders;

public sealed partial class EditFolderDialogViewModel(
    ICommandDispatcher commandDispatcher,
    IDialogService dialogService)
    : ViewModelBase,
      IDialogViewModel<Guid?>
{
    private Guid _folderId;

    private string _name =
        string.Empty;

    private string? _errorMessage;

    private bool _isSaving;

    private FolderColorOption _selectedColor =
        FolderAppearanceOptions.Colors[0];

    private FolderIconOption _selectedIcon =
        FolderAppearanceOptions.Icons[0];

    public event EventHandler<
        DialogCloseRequestedEventArgs<Guid?>>?
        CloseRequested;

    public IReadOnlyList<FolderColorOption> ColorOptions =>
        FolderAppearanceOptions.Colors;

    public IReadOnlyList<FolderIconOption> IconOptions =>
        FolderAppearanceOptions.Icons;

    public string Name
    {
        get => _name;

        set
        {
            if (!SetProperty(
                    ref _name,
                    value))
            {
                return;
            }

            ErrorMessage = null;

            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    public FolderColorOption SelectedColor
    {
        get => _selectedColor;

        set
        {
            if (!SetProperty(
                    ref _selectedColor,
                    value))
            {
                return;
            }

            ErrorMessage = null;
        }
    }

    public FolderIconOption SelectedIcon
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

            ErrorMessage = null;
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;

        private set
        {
            if (!SetProperty(
                    ref _errorMessage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasError));
        }
    }

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    public bool IsSaving
    {
        get => _isSaving;

        private set
        {
            if (!SetProperty(
                    ref _isSaving,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(IsBusy));

            SaveCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsBusy =>
        IsSaving;

    public void Initialize(
        LibraryFolderSnapshotDto folder)
    {
        ArgumentNullException.ThrowIfNull(
            folder);

        _folderId =
            folder.Id;

        Name =
            folder.Name;

        SelectedColor =
            GetColorOption(
                folder.Color);

        SelectedIcon =
            GetIconOption(
                folder.Icon);

        ErrorMessage = null;
        IsSaving = false;
    }

    [RelayCommand(
        CanExecute = nameof(CanSave))]
    private async Task SaveAsync(
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsSaving = true;

        try
        {
            var command =
                new UpdateLibraryFolderCommand(
                    _folderId,
                    Name,
                    SelectedColor.Value,
                    SelectedIcon.Value);

            var result =
                await commandDispatcher.SendAsync<
                    UpdateLibraryFolderCommand,
                    Guid>(
                    command,
                    cancellationToken);

            if (cancellationToken
                .IsCancellationRequested)
            {
                ErrorMessage =
                    "Сохранение изменений было отменено";

                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage =
                    result.Error
                        .FirstOrDefault()
                        ?.Message
                    ?? "Не удалось сохранить изменения";

                return;
            }

            CloseRequested?.Invoke(
                this,
                new DialogCloseRequestedEventArgs<Guid?>(
                    result.Value,
                    true));
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            ErrorMessage =
                "Сохранение изменений было отменено";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanSave()
    {
        return !IsSaving &&
               _folderId != Guid.Empty &&
               !string.IsNullOrWhiteSpace(
                   Name);
    }

    [RelayCommand(
        CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CloseRequested?.Invoke(
            this,
            new DialogCloseRequestedEventArgs<Guid?>(
                null,
                false));
    }

    private bool CanCancel()
    {
        return !IsSaving;
    }

    [RelayCommand]
    private void OpenIconPicker()
    {
        var selectedIcon =
            dialogService.Show<
                SelectFolderIconDialogViewModel,
                FolderIcon?>(
                viewModel =>
                    viewModel.Initialize(
                        SelectedIcon.Value));

        if (selectedIcon is null)
        {
            return;
        }

        SelectedIcon =
            IconOptions.First(
                option =>
                    option.Value ==
                    selectedIcon.Value);
    }

    public void CancelPendingOperation()
    {
        if (SaveCommand.CanBeCanceled)
        {
            SaveCommand.Cancel();
        }
    }

    private FolderColorOption GetColorOption(
        string? value)
    {
        if (!EnumStableNames.TryParseStableName<FolderColor>(
                value,
                out var color))
        {
            return ColorOptions[0];
        }

        return ColorOptions.FirstOrDefault(
                   option =>
                       option.Value == color)
               ?? ColorOptions[0];
    }

    private FolderIconOption GetIconOption(
        string? value)
    {
        if (!EnumStableNames.TryParseStableName<FolderIcon>(
                value,
                out var icon))
        {
            return IconOptions[0];
        }

        return IconOptions.FirstOrDefault(
                   option =>
                       option.Value == icon)
               ?? IconOptions[0];
    }
}