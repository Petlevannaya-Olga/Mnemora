using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Commands;
using Mnemora.Application.Topics.Delete;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Topics;

public sealed partial class DeleteTopicDialogViewModel(
    ICommandDispatcher commandDispatcher)
    : ViewModelBase,
      IDialogViewModel<bool>
{
    private Guid _topicId;

    private string _topicName = string.Empty;

    private string? _errorMessage;

    private bool _isDeleting;

    public event EventHandler<DialogCloseRequestedEventArgs<bool>>? CloseRequested;

    public string TopicName
    {
        get => _topicName;

        private set => SetProperty(ref _topicName, value);
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
        LibraryTopicDto topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        _topicId =
            topic.Id;

        TopicName =
            topic.Name;

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
                new DeleteTopicCommand(_topicId);

            var result =
                await commandDispatcher.SendAsync<
                    DeleteTopicCommand,
                    Guid>(
                    command,
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                ErrorMessage = "Удаление темы было отменено";

                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage =
                    result.Error
                        .FirstOrDefault()
                        ?.Message
                    ?? "Не удалось удалить тему";

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
            ErrorMessage = "Удаление темы было отменено";
        }
        finally
        {
            IsDeleting = false;
        }
    }

    private bool CanDelete()
    {
        return !IsDeleting && _topicId != Guid.Empty;
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