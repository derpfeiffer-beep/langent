using Langent.Core.Infrastructure;

namespace Langent.Core.Tests.TestDoubles;

public sealed class FakeDirectoryProbe : IDirectoryProbe
{
    private readonly HashSet<string> _writable;

    public FakeDirectoryProbe(params string[] writable)
        => _writable = new HashSet<string>(writable, StringComparer.OrdinalIgnoreCase);

    public bool CanWrite(string directory) => _writable.Contains(directory);

    public void EnsureExists(string directory)
    {
    }
}
