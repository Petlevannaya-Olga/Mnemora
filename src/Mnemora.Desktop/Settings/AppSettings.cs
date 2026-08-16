using System.Text.Json.Serialization;

namespace Mnemora.Desktop.Settings;

public sealed class AppSettings
{
    private LibraryOverviewViewMode _libraryOverviewViewMode = LibraryOverviewViewMode.Tiles;

    public string? UserName { get; set; }

    public string? StoragePath { get; set; }

    public bool IsAiConfigured { get; set; }

    public bool IsOnboardingCompleted { get; set; }

    public LibraryOverviewViewMode LibraryOverviewViewMode
    {
        get => _libraryOverviewViewMode;
        set
        {
            _libraryOverviewViewMode = value;
            HasExplicitLibraryOverviewViewMode = true;
        }
    }

    public LibraryManagementViewMode LibraryManagementViewMode { get; set; } = LibraryManagementViewMode.Table;

    [JsonPropertyName("libraryViewMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LibraryOverviewViewMode? LegacyLibraryViewMode { get; set; }

    [JsonIgnore]
    internal bool HasExplicitLibraryOverviewViewMode { get; private set; }

    internal void ApplyLegacySettings()
    {
        if (!HasExplicitLibraryOverviewViewMode && LegacyLibraryViewMode is { } legacyViewMode)
        {
            _libraryOverviewViewMode = legacyViewMode;
        }

        LegacyLibraryViewMode = null;
    }
}