using System.Text.Json.Serialization;
namespace Mnemora.Desktop.Notifications;

public enum NotificationKind
{
    [JsonStringEnumMemberName("Success")]
    SUCCESS,
    [JsonStringEnumMemberName("Error")]
    ERROR,
    [JsonStringEnumMemberName("Warning")]
    WARNING,
    [JsonStringEnumMemberName("Information")]
    INFORMATION
}