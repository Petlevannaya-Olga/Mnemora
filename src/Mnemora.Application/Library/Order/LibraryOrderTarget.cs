using System.Text.Json.Serialization;
namespace Mnemora.Application.Library.Order;

public enum LibraryOrderTarget
{
    [JsonStringEnumMemberName("Sections")]
    SECTIONS,
    [JsonStringEnumMemberName("Folders")]
    FOLDERS,
    [JsonStringEnumMemberName("Materials")]
    MATERIALS
}
