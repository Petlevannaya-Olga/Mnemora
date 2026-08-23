namespace Mnemora.Desktop.ViewModels.Library;

public sealed record LibraryBreadcrumbItemViewModel(
    Guid ContainerId,
    string Name,
    int Depth,
    bool IsCurrent);
