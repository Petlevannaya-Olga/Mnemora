using System.IO;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Desktop.Tests.Storage;

public sealed class DatabasePathProviderTests
{
    [Fact]
    public void CreateConnectionString_WhenStorageWasDeleted_DoesNotRecreateIt()
    {
        string storagePath = Path.Combine(
            Path.GetTempPath(),
            "Mnemora.Tests",
            Guid.NewGuid().ToString("N"));

        var result =
            DatabasePathProvider.CreateConnectionString(
                storagePath);

        Assert.True(result.IsFailure);
        Assert.False(
            Directory.Exists(storagePath));
    }
}
