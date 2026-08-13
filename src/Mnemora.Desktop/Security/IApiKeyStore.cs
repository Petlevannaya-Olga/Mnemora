namespace Mnemora.Desktop.Security;

public interface IApiKeyStore
{
    string? Load();

    void Save(string apiKey);

    void Delete();
}