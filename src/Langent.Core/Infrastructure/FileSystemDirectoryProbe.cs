namespace Langent.Core.Infrastructure;

/// <summary>
/// Default <see cref="IDirectoryProbe"/> backed by <see cref="System.IO"/>.
/// Uses only cross-platform APIs, so it may live in Core.
/// </summary>
public sealed class FileSystemDirectoryProbe : IDirectoryProbe
{
    public bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probeFile = Path.Combine(directory, $".langent-write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probeFile))
            {
            }
            File.Delete(probeFile);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    public void EnsureExists(string directory) => Directory.CreateDirectory(directory);
}
