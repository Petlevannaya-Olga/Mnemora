using System.Text.Json.Serialization;
namespace Mnemora.Application.Library.GetMaterialsPage;

public enum LibraryMaterialFilter
{
    [JsonStringEnumMemberName("All")]
    ALL,
    [JsonStringEnumMemberName("Articles")]
    ARTICLES,
    [JsonStringEnumMemberName("Questions")]
    QUESTIONS
}