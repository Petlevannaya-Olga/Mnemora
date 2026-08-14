namespace Mnemora.Desktop.ViewModels.Sections;

public sealed class SectionCreatedEventArgs(Guid sectionId) : EventArgs
{
    public Guid SectionId { get; } = sectionId;
}