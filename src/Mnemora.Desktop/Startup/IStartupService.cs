namespace Mnemora.Desktop.Startup;

public interface IStartupService
{
    Task<StartupResult> InitializeAsync(IProgress<StartupProgress> progress, CancellationToken cancellationToken = default);
}
