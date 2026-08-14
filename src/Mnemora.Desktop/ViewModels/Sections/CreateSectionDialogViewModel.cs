using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Commands;
using Mnemora.Application.Sections.Create;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Sections;

public sealed partial class CreateSectionDialogViewModel(
    ICommandDispatcher commandDispatcher)
    : ViewModelBase,
      IDialogViewModel<Guid?>
{
    private string _name = string.Empty;
    private string? _errorMessage;
    private bool _isCreating;
    private SectionColorOption _selectedColor = SectionAppearanceOptions.Colors[0];
    private SectionIconOption _selectedIcon = SectionAppearanceOptions.Icons[0];

    public event EventHandler<DialogCloseRequestedEventArgs<Guid?>>? CloseRequested;

    public IReadOnlyList<SectionColorOption> ColorOptions =>
        SectionAppearanceOptions.Colors;

    public IReadOnlyList<SectionIconOption> IconOptions =>
        SectionAppearanceOptions.Icons;

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
            OnPropertyChanged(nameof(PreviewName));
            CreateCommand.NotifyCanExecuteChanged();
        }
    }

    public string PreviewName =>
        string.IsNullOrWhiteSpace(Name)
            ? "Название раздела"
            : Name.Trim();

    public SectionColorOption SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (!SetProperty(ref _selectedColor, value))
            {
                return;
            }

            ErrorMessage = null;
        }
    }

    public SectionIconOption SelectedIcon
    {
        get => _selectedIcon;
        set
        {
            if (!SetProperty(ref _selectedIcon, value))
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
            if (!SetProperty(ref _errorMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

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

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsCreating = true;

        try
        {
            var command = new CreateSectionCommand(
                Name,
                SelectedColor.Value,
                SelectedIcon.Value);

            var result = await commandDispatcher
                .SendAsync<CreateSectionCommand, Guid>(
                    command,
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                ErrorMessage = "Создание раздела было отменено";
                return;
            }

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                    ?? "Не удалось создать раздел";

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
            ErrorMessage = "Создание раздела было отменено";
        }
        finally
        {
            IsCreating = false;
        }
    }

    private bool CanCreate()
    {
        return !IsCreating && !string.IsNullOrWhiteSpace(Name);
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

    public void CancelPendingOperation()
    {
        if (CreateCommand.CanBeCanceled)
        {
            CreateCommand.Cancel();
        }
    }
}