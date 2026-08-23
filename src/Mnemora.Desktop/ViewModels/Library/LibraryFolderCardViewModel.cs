using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibraryFolderCardViewModel
{
    public LibraryFolderCardViewModel(LibraryFolderDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
    }

    public LibraryFolderDto Source { get; }

    public Guid Id => Source.Id;
    public Guid ParentId => Source.ParentId;
    public int Depth => Source.Depth;
    public string Name => Source.Name;
    public string Color => Source.Color;
    public string Icon => Source.Icon;
    public int DisplayOrder => Source.DisplayOrder;
    public int ChildFoldersCount => Source.ChildFoldersCount;
    public int MaterialsCount => Source.MaterialsCount;
    public bool CanCreateChildFolder => Source.CanCreateChildFolder;

    public string ContentsText
    {
        get
        {
            string materials = FormatCount(
                MaterialsCount,
                "материал",
                "материала",
                "материалов");

            if (ChildFoldersCount == 0)
            {
                return materials;
            }

            string folders = FormatCount(
                ChildFoldersCount,
                "папка",
                "папки",
                "папок");

            return $"{folders} • {materials}";
        }
    }

    private static string FormatCount(
        int count,
        string one,
        string few,
        string many)
    {
        int lastTwoDigits = count % 100;

        if (lastTwoDigits is >= 11 and <= 14)
        {
            return $"{count} {many}";
        }

        return (count % 10) switch
        {
            1 => $"{count} {one}",
            2 or 3 or 4 => $"{count} {few}",
            _ => $"{count} {many}",
        };
    }
}
