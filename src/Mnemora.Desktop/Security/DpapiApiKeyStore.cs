using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Mnemora.Desktop.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiApiKeyStore
    : IApiKeyStore
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes(
            "Mnemora.OpenAI.ApiKey.v1");

    private readonly string _credentialsDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Mnemora",
            "Credentials");

    private string ApiKeyPath =>
        Path.Combine(
            _credentialsDirectory,
            "openai-api-key.bin");

    public string? Load()
    {
        if (!File.Exists(ApiKeyPath))
        {
            return null;
        }

        byte[] protectedBytes =
            File.ReadAllBytes(ApiKeyPath);

        byte[]? plainBytes = null;

        try
        {
            plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(
                plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                protectedBytes);

            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    plainBytes);
            }
        }
    }

    public void Save(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiKey);

        Directory.CreateDirectory(
            _credentialsDirectory);

        byte[] plainBytes =
            Encoding.UTF8.GetBytes(apiKey.Trim());

        byte[]? protectedBytes = null;

        string temporaryPath = Path.Combine(
            _credentialsDirectory,
            $"openai-api-key-{Guid.NewGuid():N}.tmp");

        try
        {
            protectedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            File.WriteAllBytes(
                temporaryPath,
                protectedBytes);

            File.Move(
                temporaryPath,
                ApiKeyPath,
                overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plainBytes);

            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    protectedBytes);
            }

            _ = TryDeleteFile(temporaryPath);
        }
    }

    public void Delete()
    {
        if (File.Exists(ApiKeyPath))
        {
            File.Delete(ApiKeyPath);
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return true;
            }

            File.Delete(path);

            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                      or UnauthorizedAccessException
                      or NotSupportedException)
        {
            return false;
        }
    }
}