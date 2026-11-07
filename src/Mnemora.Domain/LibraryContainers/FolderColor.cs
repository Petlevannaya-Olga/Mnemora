using System.Text.Json.Serialization;
namespace Mnemora.Domain.LibraryContainers;

public enum FolderColor
{
    [JsonStringEnumMemberName("Teal")]
    TEAL,
    [JsonStringEnumMemberName("Cyan")]
    CYAN,
    [JsonStringEnumMemberName("Blue")]
    BLUE,
    [JsonStringEnumMemberName("Indigo")]
    INDIGO,
    [JsonStringEnumMemberName("Violet")]
    VIOLET,
    [JsonStringEnumMemberName("Purple")]
    PURPLE,
    [JsonStringEnumMemberName("Pink")]
    PINK,
    [JsonStringEnumMemberName("Coral")]
    CORAL,
    [JsonStringEnumMemberName("Orange")]
    ORANGE,
    [JsonStringEnumMemberName("Green")]
    GREEN
}
