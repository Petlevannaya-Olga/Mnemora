using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Application.Storage;

public interface IStoragePathProvider
{
    Result<string, Error> GetStoragePath();
}