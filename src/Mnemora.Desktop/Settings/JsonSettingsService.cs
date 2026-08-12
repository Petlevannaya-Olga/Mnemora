using System.IO;
using System.Text;
using System.Text.Json;

namespace Mnemora.Desktop.Settings;

public sealed class JsonSettingsService : ISettingsService
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "Mnemora",
        "settings.json");

    public async Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            return await LoadInternalAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        await _lock.WaitAsync(cancellationToken);

        try
        {
            AppSettings settings =
                await LoadInternalAsync(cancellationToken);

            settings.UserName = userName.Trim();

            string? directory = Path.GetDirectoryName(_settingsPath);

            if (directory is null)
            {
                throw new InvalidOperationException(
                    "Не удалось определить папку настроек.");
            }

            Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(
                settings,
                _jsonOptions);

            string temporaryPath = _settingsPath + ".tmp";

            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(false),
                cancellationToken);

            File.Move(
                temporaryPath,
                _settingsPath,
                overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<AppSettings> LoadInternalAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        await using FileStream stream = File.OpenRead(_settingsPath);

        return await JsonSerializer.DeserializeAsync<AppSettings>(
                   stream,
                   _jsonOptions,
                   cancellationToken)
               ?? new AppSettings();
    }
}