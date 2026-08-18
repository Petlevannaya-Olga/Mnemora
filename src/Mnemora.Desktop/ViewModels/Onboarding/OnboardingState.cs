using System.Text.Json.Serialization;
using Mnemora.Desktop.Settings;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed class OnboardingState
{
    public string? UserName { get; set; }

    public string? StoragePath { get; set; }

    public MarkdownEditorType? MarkdownEditor { get; set; }

    public string? VisualStudioCodePath { get; set; }

    public string? ObsidianVaultPath { get; set; }

    [JsonIgnore]
    public bool IsVisualStudioCodeVerified { get; set; }

    [JsonIgnore]
    public bool IsObsidianVerified { get; set; }

    // Совместимость с существующим App.xaml.cs:
    // присваивание false сбрасывает обе проверки.
    [JsonIgnore]
    public bool IsMarkdownEditorVerified
    {
        get => MarkdownEditor switch
        {
            MarkdownEditorType.VisualStudioCode =>
                IsVisualStudioCodeVerified,

            MarkdownEditorType.Obsidian =>
                IsObsidianVerified,

            _ => false,
        };
        set
        {
            if (!value)
            {
                IsVisualStudioCodeVerified = false;
                IsObsidianVerified = false;
                return;
            }

            switch (MarkdownEditor)
            {
                case MarkdownEditorType.VisualStudioCode:
                    IsVisualStudioCodeVerified = true;
                    break;

                case MarkdownEditorType.Obsidian:
                    IsObsidianVerified = true;
                    break;
            }
        }
    }

    public bool IsAiConfigured { get; set; }

    [JsonIgnore]
    public string? PendingApiKey { get; set; }
    
    public bool IsOnboardingCompleted { get; set; }
}