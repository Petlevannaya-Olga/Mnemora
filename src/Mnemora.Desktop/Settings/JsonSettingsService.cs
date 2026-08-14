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

    public Task SaveLibraryViewModeAsync(
        LibraryViewMode viewMode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return UpdateAsync(
            settings =>
                settings.LibraryViewMode =
                    viewMode,
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

        return await JsonSerializer
                   .DeserializeAsync<AppSettings>(
                       stream,
                       _jsonOptions,
                       cancellationToken)
               ?? new AppSettings();
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