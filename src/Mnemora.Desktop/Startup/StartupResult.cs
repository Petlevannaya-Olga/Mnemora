namespace Mnemora.Desktop.Startup;

public sealed record StartupResult(bool IsSuccess, bool WasOnboardingCompleted, bool StorageIsConfigured, bool EditorIsConfigured, string? ErrorMessage)
{
    public static StartupResult Success(bool wasOnboardingCompleted, bool storageIsConfigured, bool editorIsConfigured) => new(true, wasOnboardingCompleted, storageIsConfigured, editorIsConfigured, null);

    public static StartupResult Failure(string errorMessage) => new(false, false, false, false, errorMessage);
}
