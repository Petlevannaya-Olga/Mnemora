using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Commands;
using Mnemora.Application.LibraryContainers.Delete;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Folders;

public sealed partial class DeleteFolderDialogViewModel(
    ICommandDispatcher commandDispatcher)
    : ViewModelBase,
      IDialogViewModel<bool>
{
    private Guid _folderId;

    private string _folderName = string.Empty;

    private string? _errorMessage;

    private bool _isDeleting;

    public event EventHandler<DialogCloseRequestedEventArgs<bool>>? CloseRequested;

    public string FolderName
    {
        get => _folderName;

        private set => SetProperty(ref _folderName, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;

        private set
        {
            if (!SetProperty(ref _errorMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsDeleting
    {
        get => _isDeleting;

        private set
        {
            if (!SetProperty(ref _isDeleting, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsBusy));

            DeleteCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsBusy =>
        IsDeleting;

    public void Initialize(
        LibraryFolderSnapshotDto folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        _folderId =
            folder.Id;

        FolderName =
            folder.Name;

        ErrorMessage = null;
        IsDeleting = false;
    }

    [RelayCommand(
        CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync(
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsDeleting = true;

        try
        {
            var command =
                new DeleteLibraryFolderCommand(_folderId);

            var result =
                await commandDispatcher.SendAsync<
                    DeleteLibraryFolderCommand,
                    Guid>(
                    command,
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                ErrorMessage = "Удаление папки было отменено";

                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage =
                    result.Error
                        .FirstOrDefault()
                        ?.Message
                    ?? "Не удалось удалить папку";

                return;
            }

            CloseRequested?.Invoke(
                this,
                new DialogCloseRequestedEventArgs<bool>(
                    true,
                    true));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "Удаление папки было отменено";
        }
        finally
        {
            IsDeleting = false;
        }
    }

    private bool CanDelete()
    {
        return !IsDeleting && _folderId != Guid.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CloseRequested?.Invoke(
            this,
            new DialogCloseRequestedEventArgs<bool>(
                false,
                false));
    }

    private bool CanCancel()
    {
        return !IsDeleting;
    }

    public void CancelPendingOperation()
    {
        if (DeleteCommand.CanBeCanceled)
        {
            DeleteCommand.Cancel();
        }
    }
}