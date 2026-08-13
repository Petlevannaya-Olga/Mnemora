namespace Mnemora.Desktop.Storage;

public interface IFolderPickerService
{
    string? SelectFolder(string? initialDirectory = null);
}