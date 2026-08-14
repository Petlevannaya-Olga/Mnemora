using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Commands;
using Mnemora.Application.Sections.Create;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Sections;

public sealed partial class CreateSectionDialogViewModel(
    ICommandDispatcher commandDispatcher)
    : ViewModelBase
{
    private string _name = string.Empty;
    private string? _errorMessage;
    private bool _isCreating;

    public event EventHandler? CancelRequested;

    public event EventHandler<SectionCreatedEventArgs>? SectionCreated;

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

            CreateCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanCreate()
    {
        return !IsCreating;
    }

    private bool CanCancel()
    {
        return !IsCreating;
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsCreating = true;

        try
        {
            var command = new CreateSectionCommand(Name);

            var result = await commandDispatcher
                .SendAsync<CreateSectionCommand, Guid>(
                    command,
                    cancellationToken);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                    ?? "Не удалось создать раздел";

                return;
            }

            SectionCreated?.Invoke(
                this,
                new SectionCreatedEventArgs(result.Value));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "Создание раздела было отменено";
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}