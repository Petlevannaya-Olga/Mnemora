using System.Collections;

namespace Mnemora.Shared;

public sealed class Errors : IReadOnlyCollection<Error>
{
    private readonly List<Error> _errors;

    public Errors(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        _errors = [.. errors];
    }

    public int Count => _errors.Count;

    public IEnumerator<Error> GetEnumerator()
    {
        return _errors.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static implicit operator Errors(Error[] errors)
    {
        return new Errors(errors);
    }

    public static implicit operator Errors(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Errors([error]);
    }
}