using System.Text.Json.Serialization;
namespace Mnemora.Domain.Materials;

public enum MaterialType
{
    [JsonStringEnumMemberName("Article")]
    ARTICLE = 1,
    [JsonStringEnumMemberName("Question")]
    QUESTION = 2
}