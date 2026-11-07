using System.Text.Json.Serialization;
namespace Mnemora.Desktop.Settings;

#pragma warning disable CA1707
public enum MarkdownEditorType
{
    [JsonStringEnumMemberName("VisualStudioCode")]
    VISUAL_STUDIO_CODE,
    [JsonStringEnumMemberName("Obsidian")]
    OBSIDIAN,
}
#pragma warning restore CA1707
