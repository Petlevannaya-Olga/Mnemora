namespace Mnemora.Desktop.Settings;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default);

    Task SaveStoragePathAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task CompleteOnboardingAsync(
        bool isAiConfigured,
        CancellationToken cancellationToken = default);
}