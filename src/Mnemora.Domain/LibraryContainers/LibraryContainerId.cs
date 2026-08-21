using CSharpFunctionalExtensions;
using Mnemora.Shared;

namespace Mnemora.Domain.LibraryContainers;

public sealed record LibraryContainerId
{
    public Guid Value { get; }

    private LibraryContainerId(Guid value)
    {
        Value = value;
    }

    public static LibraryContainerId New() => new(Guid.NewGuid());

    public static Result<LibraryContainerId, Error> Create(Guid containerId)
    {
        if (containerId == Guid.Empty)
        {
            return CommonErrors.IsEmpty(nameof(containerId));
        }

        return new LibraryContainerId(containerId);
    }
}
