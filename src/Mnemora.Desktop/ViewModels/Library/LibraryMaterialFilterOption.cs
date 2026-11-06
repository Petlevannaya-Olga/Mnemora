using Mnemora.Application.Library.GetMaterialsPage;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed record LibraryMaterialFilterOption(
    string Name,
    LibraryMaterialFilter Filter);
