using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Editors;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class CreateMaterialViewModel(
    IMarkdownEditorService markdownEditorService,
    ISettingsService settingsService)
    : ViewModelBase
{
    private const string MaterialsDirectoryName =
        "materials";

    private const string DraftsDirectoryName =
        "_drafts";

    private Action? _closeRequested;

    [ObservableProperty]
    private LibraryManagementOrderItemViewModel? _selectedTopic;

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
        _closeRequested = null;
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
    private void CancelCreateMaterial()
    {
        Closing?.Invoke(this, EventArgs.Empty);
        _closeRequested?.Invoke();
    }
}
