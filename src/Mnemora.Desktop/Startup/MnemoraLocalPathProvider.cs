using System.IO;

namespace Mnemora.Desktop.Startup;

public sealed class MnemoraLocalPathProvider : IMnemoraLocalPathProvider
{
    public string RootPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mnemora");
    public string TempPath => Path.Combine(RootPath, "Temp");
    public string StagingPath => Path.Combine(RootPath, "Staging");
}
