namespace Mnemora.Desktop.Ai;

public sealed record AiConnectionCheckResult(
    bool IsSuccess,
    string Message,
    bool ShouldPersist)
{
    public static AiConnectionCheckResult Connected(
        string message = "API-ключ принят OpenAI.",
        bool shouldPersist = true)
    {
        return new AiConnectionCheckResult(
            IsSuccess: true,
            message,
            shouldPersist);
    }

    public static AiConnectionCheckResult Failed(
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new AiConnectionCheckResult(
            IsSuccess: false,
            message,
            ShouldPersist: false);
    }
}