namespace Mnemora.Desktop.Notifications;

public sealed record NotificationMessage(
    Guid Id,
    string Text,
    NotificationKind Kind,
    DateTimeOffset ExpiresAt);