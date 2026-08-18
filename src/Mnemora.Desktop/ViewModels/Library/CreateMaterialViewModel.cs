using System.IO;
using MaterialDesignThemes.Wpf;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Topics;
using Mnemora.Domain.Topics;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class CreateMaterialViewModel(
    IMarkdownEditorService markdownEditorService,
    ISettingsService settingsService,
    IDialogService dialogService)
    : ViewModelBase
{
    private const string MaterialsDirectoryName =
        "materials";

    private const string DraftsDirectoryName =
        "_drafts";

    private const PackIconKind DefaultMaterialIconKind =
        PackIconKind.FileDocumentOutline;

    private Action? _closeRequested;

    [ObservableProperty]
    private LibraryManagementOrderItemViewModel? _selectedTopic;

    [ObservableProperty]
    private PackIconKind _selectedIconKind =
        DefaultMaterialIconKind;

    public string SelectedIconKey =>
        SelectedIconKind.ToString();

    public event EventHandler? Closing;

    public void Initialize(
        LibraryManagementOrderItemViewModel selectedTopic,
        Action closeRequested)
    {
        ArgumentNullException.ThrowIfNull(selectedTopic);
        ArgumentNullException.ThrowIfNull(closeRequested);

        SelectedTopic = selectedTopic;
        _closeRequested = closeRequested;
    }

    public void Reset()
    {
        SelectedTopic = null;
        SelectedIconKind = DefaultMaterialIconKind;
        _closeRequested = null;
    }

    partial void OnSelectedIconKindChanged(
        PackIconKind value)
    {
        OnPropertyChanged(nameof(SelectedIconKey));
    }

    public Task<MarkdownEditorLaunchResult> OpenMarkdownAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return markdownEditorService.OpenAsync(
            filePath,
            cancellationToken);
    }

    public async Task<string> GetDraftDirectoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException(
                "Не указан идентификатор сессии создания материала.",
                nameof(sessionId));
        }

        AppSettings settings =
            await settingsService.LoadAsync(
                cancellationToken);

        if (string.IsNullOrWhiteSpace(settings.StoragePath))
        {
            throw new InvalidOperationException(
                "Хранилище Mnemora не настроено.");
        }

        string storagePath =
            Path.GetFullPath(
                settings.StoragePath.Trim());

        return Path.Combine(
            storagePath,
            MaterialsDirectoryName,
            DraftsDirectoryName,
            "create-material",
            sessionId);
    }


    [RelayCommand]
    private void OpenIconPicker()
    {
        var currentOption =
            TopicAppearanceOptions.Icons
                .FirstOrDefault(option =>
                    option.Kind == SelectedIconKind)
            ?? TopicAppearanceOptions.Icons[0];

        var selectedIcon = dialogService
            .Show<SelectTopicIconDialogViewModel, TopicIcon?>(
                viewModel =>
                    viewModel.Initialize(currentOption.Value));

        if (selectedIcon is null)
        {
            return;
        }

        var selectedOption =
            TopicAppearanceOptions.Icons
                .FirstOrDefault(option =>
                    option.Value == selectedIcon.Value);

        if (selectedOption is null)
        {
            return;
        }

        SelectedIconKind = selectedOption.Kind;
    }

    [RelayCommand]
    private void CancelCreateMaterial()
    {
        Closing?.Invoke(this, EventArgs.Empty);
        _closeRequested?.Invoke();
    }
}
