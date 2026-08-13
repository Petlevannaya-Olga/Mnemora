namespace Mnemora.Desktop.Ai;

public interface IAiConnectionService
{
    Task<AiConnectionCheckResult> CheckAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}