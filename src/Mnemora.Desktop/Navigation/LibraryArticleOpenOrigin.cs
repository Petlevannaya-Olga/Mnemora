using System.Text.Json.Serialization;
namespace Mnemora.Desktop.Navigation;

public enum LibraryArticleOpenOrigin
{
    [JsonStringEnumMemberName("Overview")]
    OVERVIEW,
    [JsonStringEnumMemberName("Management")]
    MANAGEMENT,
}
