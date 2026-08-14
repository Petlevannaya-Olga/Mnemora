using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Commands;
using Mnemora.Application.Topics.Create;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Domain.Topics;

namespace Mnemora.Desktop.ViewModels.Topics;

public sealed partial class CreateTopicDialogViewModel(
    ICommandDispatcher commandDispatcher,
    IDialogService dialogService)
    : ViewModelBase,
      IDialogViewModel<Guid?>
{
    private Guid _sectionId;
    private string _sectionName = string.Empty;
    private string _name = string.Empty;
    private string? _errorMessage;
    private bool _isCreating;

    private TopicColorOption _selectedColor =
        TopicAppearanceOptions.Colors[0];

    private TopicIconOption _selectedIcon =
        TopicAppearanceOptions.Icons[0];

    public event EventHandler<DialogCloseRequestedEventArgs<Guid?>>?
        CloseRequested;

    public IReadOnlyList<TopicColorOption> ColorOptions =>
        TopicAppearanceOptions.Colors;

    public IReadOnlyList<TopicIconOption> IconOptions =>
        TopicAppearanceOptions.Icons;

    public string SectionName
    {
        get => _sectionName;
        private set => SetProperty(
            ref _sectionName,
            value);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (!SetProperty(ref _name, value))
            {
                return;
            }

            ErrorMessage = null;
            CreateCommand.NotifyCanExecuteChanged();
        }
    }

    public TopicColorOption SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (SetProperty(ref _selectedColor, value))
            {
                ErrorMessage = null;
            }
        }
    }

    public TopicIconOption SelectedIcon
    {
        get => _selectedIcon;
        set
        {
            if (SetProperty(ref _selectedIcon, value))
            {
                ErrorMessage = null;
            }
        }
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

    public bool HasError =>
        !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsCreating
    {
        get => _isCreating;
        private set
        {
            if (!SetProperty(ref _isCreating, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsBusy));
            CreateCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsBusy => IsCreating;

    public void Initialize(
        Guid sectionId,
        string sectionName)
    {
        if (sectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Идентификатор раздела не может быть пустым.",
                nameof(sectionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sectionName);

        _sectionId = sectionId;
        SectionName = sectionName;
        Name = string.Empty;

        SelectedColor =
            TopicAppearanceOptions.Colors[0];

        SelectedIcon =
            TopicAppearanceOptions.Icons[0];

        ErrorMessage = null;
        IsCreating = false;
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync(
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsCreating = true;

        try
        {
            var command = new CreateTopicCommand(
                _sectionId,
                Name,
                SelectedColor.Value,
                SelectedIcon.Value);

            var result = await commandDispatcher
                .SendAsync<CreateTopicCommand, Guid>(
                    command,
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                ErrorMessage =
                    "Создание темы было отменено";

                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage =
                    result.Error.FirstOrDefault()?.Message
                    ?? "Не удалось создать тему";

                return;
            }

            CloseRequested?.Invoke(
                this,
                new DialogCloseRequestedEventArgs<Guid?>(
                    result.Value,
                    true));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            ErrorMessage =
                "Создание темы было отменено";
        }
        finally
        {
            IsCreating = false;
        }
    }

    private bool CanCreate()
    {
        return _sectionId != Guid.Empty
               && !IsCreating
               && !string.IsNullOrWhiteSpace(Name);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
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
        return !IsCreating;
    }

    [RelayCommand]
    private void OpenIconPicker()
    {
        var selectedIcon = dialogService.Show<
            SelectTopicIconDialogViewModel,
            TopicIcon?>(
            viewModel => viewModel.Initialize(
                SelectedIcon.Value));

        if (selectedIcon is null)
        {
            return;
        }

        SelectedIcon =
            IconOptions.FirstOrDefault(
                option =>
                    option.Value == selectedIcon.Value)
            ?? IconOptions[0];
    }

    public void CancelPendingOperation()
    {
        if (CreateCommand.CanBeCanceled)
        {
            CreateCommand.Cancel();
        }
    }
}