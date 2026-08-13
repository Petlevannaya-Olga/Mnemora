namespace Mnemora.Desktop.Settings;

public sealed class AppSettings
{
    public string? UserName { get; set; }

    public string? StoragePath { get; set; }

    public bool IsAiConfigured { get; set; }

    public bool IsOnboardingCompleted { get; set; }
}