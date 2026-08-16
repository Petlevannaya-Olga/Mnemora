using Mnemora.Shared;

namespace Mnemora.Infrastructure.Persistence;

internal sealed class PersistenceConfigurationException : Exception
{
    public Error Error { get; }

    public PersistenceConfigurationException(Error error) : base(error.Message)
    {
        Error = error;
    }
}