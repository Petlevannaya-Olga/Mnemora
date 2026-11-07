using System.Text.Json.Serialization;
namespace Mnemora.Domain.Materials;

public enum MaterialDifficulty
{
    [JsonStringEnumMemberName("Easy")]
    EASY = 1,
    [JsonStringEnumMemberName("Medium")]
    MEDIUM = 2,
    [JsonStringEnumMemberName("Hard")]
    HARD = 3
}