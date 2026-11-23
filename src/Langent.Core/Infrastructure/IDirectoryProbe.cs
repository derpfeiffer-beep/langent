namespace Langent.Core.Infrastructure;

/// <summary>
/// Abstraction over the few file-system operations the path resolver needs,
/// so the writability decision can be unit-tested without touching a real disk.
/// </summary>
public interface IDirectoryProbe
{
    /// <summary>
    /// Returns true if a file can be created and deleted in <paramref name="directory"/>.
    /// The directory is created first if it does not yet exist.
    /// </summary>
    bool CanWrite(string directory);

    /// <summary>Creates the directory (and parents) if it does not already exist.</summary>
    void EnsureExists(string directory);
}
