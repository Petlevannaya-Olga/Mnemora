namespace Mnemora.Desktop.Startup;

public sealed record StartupProgress(int Percent, string Title, string? Details = null);
