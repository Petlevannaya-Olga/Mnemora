namespace Mnemora.Desktop.Startup;

public interface IMnemoraLocalPathProvider
{
    string RootPath { get; }
    string TempPath { get; }
}
