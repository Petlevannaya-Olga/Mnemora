using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Commands;
using Mnemora.Application.Topics.Update;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Domain.Topics;

namespace Mnemora.Desktop.ViewModels.Topics;

public sealed partial class EditTopicDialogViewModel(
    ICommandDispatcher commandDispatcher,
    IDialogService dialogService)
    : ViewModelBase,
      IDialogViewModel<Guid?>
{
    private Guid _topicId;

    private string _name =
        string.Empty;

    private string? _errorMessage;

    private bool _isSaving;

    private TopicColorOption _selectedColor =
        TopicAppearanceOptions.Colors[0];

    private TopicIconOption _selectedIcon =
        TopicAppearanceOptions.Icons[0];

    public event EventHandler<
        DialogCloseRequestedEventArgs<Guid?>>?
        CloseRequested;

    public IReadOnlyList<TopicColorOption> ColorOptions =>
        TopicAppearanceOptions.Colors;

    public IReadOnlyList<TopicIconOption> IconOptions =>
        TopicAppearanceOptions.Icons;

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

    public TopicColorOption SelectedColor
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

    public TopicIconOption SelectedIcon
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
        LibraryTopicDto topic)
    {
        ArgumentNullException.ThrowIfNull(
            topic);

        _topicId =
            topic.Id;

        Name =
            topic.Name;

        SelectedColor =
            GetColorOption(
                topic.Color);

        SelectedIcon =
            GetIconOption(
                topic.Icon);

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
                new UpdateTopicCommand(
                    _topicId,
                    Name,
                    SelectedColor.Value,
                    SelectedIcon.Value);

            var result =
                await commandDispatcher.SendAsync<
                    UpdateTopicCommand,
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
               _topicId != Guid.Empty &&
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
                SelectTopicIconDialogViewModel,
                TopicIcon?>(
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

    private TopicColorOption GetColorOption(
        string? value)
    {
        if (!Enum.TryParse<TopicColor>(
                value,
                ignoreCase: true,
                out var color))
        {
            return ColorOptions[0];
        }

        return ColorOptions.FirstOrDefault(
                   option =>
                       option.Value == color)
               ?? ColorOptions[0];
    }

    private TopicIconOption GetIconOption(
        string? value)
    {
        if (!Enum.TryParse<TopicIcon>(
                value,
                ignoreCase: true,
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