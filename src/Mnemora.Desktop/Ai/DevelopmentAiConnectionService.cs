namespace Mnemora.Desktop.Ai;

public sealed class DevelopmentAiConnectionService
    : IAiConnectionService
{
    public const string SuccessfulTestKey = "mnemora-test-ok";

    public async Task<AiConnectionCheckResult> CheckAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        await Task.Delay(
            TimeSpan.FromMilliseconds(700),
            cancellationToken);

        return string.Equals(
            apiKey.Trim(),
            SuccessfulTestKey,
            StringComparison.Ordinal)
            ? AiConnectionCheckResult.Connected(
                "Тестовое подключение установлено.",
                shouldPersist: false)
            : AiConnectionCheckResult.Failed(
                "Тестовый сервис отклонил ключ.");
    }
}