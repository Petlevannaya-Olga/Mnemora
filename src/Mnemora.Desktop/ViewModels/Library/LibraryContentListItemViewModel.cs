namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibraryContentListItemViewModel
{
    public LibraryContentListItemViewModel(LibraryFolderCardViewModel folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        Folder = folder;
    }

    public LibraryContentListItemViewModel(LibraryMaterialListItemViewModel material)
    {
        ArgumentNullException.ThrowIfNull(material);
        Material = material;
    }

    public LibraryFolderCardViewModel? Folder { get; }
    public LibraryMaterialListItemViewModel? Material { get; }

    public bool IsFolder => Folder is not null;
    public bool IsMaterial => Material is not null;
    public bool IsArticle => Material?.IsArticle == true;
    public bool IsQuestion => Material?.IsQuestion == true;

    public string Title => Folder?.Name ?? Material?.Title ?? string.Empty;
    public string TypeTitle => IsFolder ? "Папка" : Material?.TypeTitle ?? string.Empty;
    public string DetailsText => Folder?.ContentsText ?? Material?.TagsText ?? string.Empty;

    public string? FolderColor => Folder?.Color;
    public string? FolderIcon => Folder?.Icon;

    public string DifficultyTitle => Material?.DifficultyTitle ?? string.Empty;
    public bool IsEasy => Material?.IsEasy == true;
    public bool IsMedium => Material?.IsMedium == true;
    public bool IsHard => Material?.IsHard == true;

    public int StudyPoints => Material?.StudyPoints ?? 0;
    public int ReviewPoints => Material?.ReviewPoints ?? 0;

    public DateTime? CreatedAtLocal => Material?.CreatedAtLocal;
    public DateTime? UpdatedAtLocal => Material?.UpdatedAtLocal;
}
