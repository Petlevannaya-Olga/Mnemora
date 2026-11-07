namespace Mnemora.Desktop.Ai;

public sealed class DevelopmentAiConnectionService
    : IAiConnectionService
{
    #pragma warning disable CA1707
    public const string SUCCESSFUL_TEST_KEY = "mnemora-test-ok";
    #pragma warning restore CA1707

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
            SUCCESSFUL_TEST_KEY,
            StringComparison.Ordinal)
            ? AiConnectionCheckResult.Connected(
                "Тестовое подключение установлено.",
                shouldPersist: true)
            : AiConnectionCheckResult.Failed(
                "Тестовый сервис отклонил ключ.");
    }
}