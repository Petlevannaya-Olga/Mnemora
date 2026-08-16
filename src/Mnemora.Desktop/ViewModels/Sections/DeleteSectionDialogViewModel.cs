using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Commands;
using Mnemora.Application.Sections.Delete;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Shared;

namespace Mnemora.Desktop.ViewModels.Sections;

public sealed partial class DeleteSectionDialogViewModel(
    ICommandDispatcher commandDispatcher)
    : ViewModelBase,
      IDialogViewModel<bool>
{
    private Guid _sectionId;

    private string _sectionName =
        string.Empty;

    private int _topicsCount;

    private string? _errorMessage;

    private bool _isDeleting;

    public event EventHandler<
        DialogCloseRequestedEventArgs<bool>>?
        CloseRequested;

    public string SectionName
    {
        get => _sectionName;

        private set =>
            SetProperty(
                ref _sectionName,
                value);
    }

    public int TopicsCount
    {
        get => _topicsCount;

        private set
        {
            if (!SetProperty(
                    ref _topicsCount,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasTopics));

            OnPropertyChanged(
                nameof(TopicsMessage));

            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasTopics =>
        TopicsCount > 0;

    public string TopicsMessage =>
        $"В разделе {TopicsCount} " +
        $"{DeclensionGenerator.Generate(
            TopicsCount,
            "тема",
            "темы",
            "тем")}. " +
        "Сначала перенесите или удалите их.";

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

    public bool IsDeleting
    {
        get => _isDeleting;

        private set
        {
            if (!SetProperty(
                    ref _isDeleting,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(IsBusy));

            DeleteCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsBusy =>
        IsDeleting;

    public void Initialize(
        LibrarySectionDto section)
    {
        ArgumentNullException.ThrowIfNull(
            section);

        _sectionId =
            section.Id;

        SectionName =
            section.Name;

        TopicsCount =
            section.Topics.Count;

        ErrorMessage = null;
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
                new DeleteSectionCommand(
                    _sectionId);

            var result =
                await commandDispatcher.SendAsync<
                    DeleteSectionCommand,
                    Guid>(
                    command,
                    cancellationToken);

            if (cancellationToken
                .IsCancellationRequested)
            {
                ErrorMessage =
                    "Удаление раздела было отменено";

                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage =
                    result.Error
                        .FirstOrDefault()
                        ?.Message
                    ?? "Не удалось удалить раздел";

                return;
            }

            CloseRequested?.Invoke(
                this,
                new DialogCloseRequestedEventArgs<bool>(
                    true,
                    true));
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            ErrorMessage =
                "Удаление раздела было отменено";
        }
        finally
        {
            IsDeleting = false;
        }
    }

    private bool CanDelete()
    {
        return !IsDeleting &&
               !HasTopics &&
               _sectionId != Guid.Empty;
    }

    [RelayCommand(
        CanExecute = nameof(CanCancel))]
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