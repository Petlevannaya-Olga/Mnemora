using System.Text.Json.Serialization;

namespace Mnemora.Desktop.ViewModels.Onboarding;

public sealed class OnboardingState
{
    public string? UserName { get; set; }

    public string? StoragePath { get; set; }

    public bool IsAiConfigured { get; set; }

    [JsonIgnore]
    public string? PendingApiKey { get; set; }
    
    public bool IsOnboardingCompleted { get; set; }
}