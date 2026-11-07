using System.Text.Json.Serialization;
namespace Mnemora.Desktop.Settings;

public enum LibraryManagementSectionColumn
{
    [JsonStringEnumMemberName("Folders")]
    FOLDERS,
    [JsonStringEnumMemberName("Materials")]
    MATERIALS,
    [JsonStringEnumMemberName("Articles")]
    ARTICLES,
    [JsonStringEnumMemberName("Questions")]
    QUESTIONS,
    [JsonStringEnumMemberName("Created")]
    CREATED,
    [JsonStringEnumMemberName("Updated")]
    UPDATED,
    [JsonStringEnumMemberName("Activity")]
    ACTIVITY,
}

public enum LibraryManagementContentColumn
{
    [JsonStringEnumMemberName("Type")]
    TYPE,
    [JsonStringEnumMemberName("Location")]
    LOCATION,
    [JsonStringEnumMemberName("Questions")]
    QUESTIONS,
    [JsonStringEnumMemberName("Difficulty")]
    DIFFICULTY,
    [JsonStringEnumMemberName("Updated")]
    UPDATED,
}
