using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Mnemora.Desktop.Settings;

public sealed class JsonSettingsService :
    ISettingsService,
    IDisposable
{
    private readonly SemaphoreSlim _semaphore =
        new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,

            WriteIndented = true,

            Encoder =
                JavaScriptEncoder.Create(
                    UnicodeRanges.All),

            Converters =
            {
                new JsonStringEnumConverter(),
            },
        };

    private readonly string _settingsDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Mnemora");

    private readonly string _settingsPath;

    private int _disposed;

    public JsonSettingsService()
    {
        _settingsPath = Path.Combine(
            _settingsDirectory,
            "settings.json");
    }

    public async Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _semaphore.WaitAsync(
            cancellationToken);

        try
        {
            return await LoadInternalAsync(
                cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task SaveUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            userName);

        string normalizedUserName =
            userName.Trim();

        return UpdateAsync(
            settings =>
                settings.UserName =
                    normalizedUserName,
            cancellationToken);
    }

    public Task SaveStoragePathAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            storagePath);

        string normalizedStoragePath =
            Path.GetFullPath(
                storagePath.Trim());

        return UpdateAsync(
            settings =>
                settings.StoragePath =
                    normalizedStoragePath,
            cancellationToken);
    }

    public Task SaveMarkdownEditorAsync(
        MarkdownEditorType? editor,
        string? visualStudioCodePath,
        string? obsidianVaultPath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings =>
            {
                settings.MarkdownEditor = editor;
                settings.VisualStudioCodePath = visualStudioCodePath;
                settings.ObsidianVaultPath = obsidianVaultPath;
            },
            cancellationToken);
    }

    public Task SaveLibraryOverviewViewModeAsync(
        LibraryOverviewViewMode viewMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings => settings.LibraryOverviewViewMode = viewMode,
            cancellationToken);
    }
    
    public Task SaveLibraryManagementViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings => settings.LibraryManagementViewMode = viewMode,
            cancellationToken);
    }

    public Task SaveLibraryManagementSectionsViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings => settings.LibraryManagementSectionsViewMode = viewMode,
            cancellationToken);
    }

    public Task SaveLibraryManagementTopicsViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings => settings.LibraryManagementTopicsViewMode = viewMode,
            cancellationToken);
    }

    public Task SaveLibraryManagementMaterialsViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings => settings.LibraryManagementMaterialsViewMode = viewMode,
            cancellationToken);
    }

    public Task SaveLibraryTopicsViewModeAsync(
        LibraryTopicsViewMode viewMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings => settings.LibraryTopicsViewMode = viewMode,
            cancellationToken);
    }

    public Task SaveLibraryTilesPerRowAsync(
        int? tilesPerRow,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (tilesPerRow is not null &&
            (tilesPerRow.Value < 2 || tilesPerRow.Value > 7))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tilesPerRow),
                tilesPerRow,
                "Количество плиток в строке должно быть от 2 до 7 или null для автоматического режима.");
        }

        return UpdateAsync(
            settings => settings.LibraryTilesPerRow = tilesPerRow,
            cancellationToken);
    }

    public Task SaveLibraryContainerFoldersPaneRatioAsync(
        double foldersPaneRatio,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!double.IsFinite(foldersPaneRatio) ||
            foldersPaneRatio is < 0.1 or > 0.9)
        {
            throw new ArgumentOutOfRangeException(
                nameof(foldersPaneRatio),
                foldersPaneRatio,
                "Доля области папок должна быть от 0.1 до 0.9.");
        }

        return UpdateAsync(
            settings => settings.LibraryContainerFoldersPaneRatio = foldersPaneRatio,
            cancellationToken);
    }

    public Task SaveLibraryManagementSectionSortAsync(
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings => settings.LibraryManagementSectionSort = sortMode,
            cancellationToken);
    }

    public Task SaveLibraryManagementTopicSortAsync(
        Guid sectionId,
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings =>
            {
                settings.LibraryManagementTopicSortBySection ??= [];
                settings.LibraryManagementTopicSortBySection[sectionId] = sortMode;
            },
            cancellationToken);
    }

    public Task SaveLibraryManagementMaterialSortAsync(
        Guid topicId,
        LibraryManagementSortMode sortMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings =>
            {
                settings.LibraryManagementMaterialSortByTopic ??= [];
                settings.LibraryManagementMaterialSortByTopic[topicId] = sortMode;
            },
            cancellationToken);
    }

    public Task CompleteOnboardingAsync(
        bool isAiConfigured,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings =>
            {
                settings.IsAiConfigured =
                    isAiConfigured;

                settings.IsOnboardingCompleted =
                    true;
            },
            cancellationToken);
    }

    private async Task UpdateAsync(
        Action<AppSettings> update,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            update);

        await _semaphore.WaitAsync(
            cancellationToken);

        try
        {
            AppSettings settings =
                await LoadInternalAsync(
                    cancellationToken);

            update(settings);

            await SaveInternalAsync(
                settings,
                cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<AppSettings> LoadInternalAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        await using FileStream stream = new(
            _settingsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);

        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
            stream,
            _jsonOptions,
            cancellationToken) ?? new AppSettings();

        settings.ApplyLegacySettings();

        return settings;
    }

    private async Task SaveInternalAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(
            _settingsDirectory);

        string json =
            JsonSerializer.Serialize(
                settings,
                _jsonOptions);

        string temporaryPath =
            Path.Combine(
                _settingsDirectory,
                $"settings-{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            File.Move(
                temporaryPath,
                _settingsPath,
                overwrite: true);
        }
        finally
        {
            _ = TryDeleteFile(
                temporaryPath);
        }
    }

    private static bool TryDeleteFile(
        string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return true;
            }

            File.Delete(path);

            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or NotSupportedException)
        {
            return false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) != 0)
        {
            return;
        }

        _semaphore.Dispose();
    }
}
