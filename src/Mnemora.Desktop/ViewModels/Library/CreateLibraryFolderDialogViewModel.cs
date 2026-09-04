using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Commands;
using Mnemora.Application.LibraryContainers.Create;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Domain.LibraryContainers;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class CreateLibraryFolderDialogViewModel(
    ICommandDispatcher commandDispatcher)
    : ViewModelBase,
      IDialogViewModel<Guid?>
{
    private Guid _parentContainerId;
    private FolderColor _color = FolderColor.Teal;
    private FolderIcon _icon = FolderIcon.Folder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _parentName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isCreating;

    public event EventHandler<DialogCloseRequestedEventArgs<Guid?>>? CloseRequested;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsBusy => IsCreating;

    public void Initialize(
        Guid parentContainerId,
        string parentName,
        string? color)
    {
        _parentContainerId = parentContainerId;
        ParentName = parentName;
        Name = string.Empty;
        ErrorMessage = null;
        _color = Enum.TryParse(color, true, out FolderColor folderColor)
            ? folderColor
            : FolderColor.Teal;
        _icon = FolderIcon.Folder;
    }

    partial void OnNameChanged(string value)
    {
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsCreating = true;

        try
        {
            var result = await commandDispatcher.SendAsync<CreateLibraryFolderCommand, Guid>(
                new CreateLibraryFolderCommand(
                    _parentContainerId,
                    Name,
                    _color,
                    _icon),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                               ?? "Не удалось создать папку";
                return;
            }

            CloseRequested?.Invoke(
                this,
                new DialogCloseRequestedEventArgs<Guid?>(
                    result.Value,
                    true));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ignore
        }
        finally
        {
            IsCreating = false;
        }
    }

    private bool CanCreate() =>
        !IsCreating &&
        _parentContainerId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CloseRequested?.Invoke(
            this,
            new DialogCloseRequestedEventArgs<Guid?>(
                null,
                false));
    }

    private bool CanCancel() => !IsCreating;

    public void CancelPendingOperation()
    {
        if (CreateCommand.CanBeCanceled)
        {
            CreateCommand.Cancel();
        }
    }
}
