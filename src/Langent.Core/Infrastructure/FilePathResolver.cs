namespace Langent.Core.Infrastructure;

/// <summary>
/// Decides where runtime files (settings.json, usage.csv, log.txt) live.
/// SPEC §6: prefer the directory next to the exe; if it is not writable, fall
/// back to <c>%APPDATA%\Langent\</c>. The same rule applies to all runtime files.
/// </summary>
public sealed class FilePathResolver
{
    private readonly IDirectoryProbe _probe;

    public FilePathResolver(IDirectoryProbe probe) => _probe = probe;

    /// <summary>
    /// Returns <paramref name="exeDirectory"/> if it is writable, otherwise the
    /// (created) <paramref name="appDataDirectory"/>.
    /// </summary>
    public string ResolveBaseDirectory(string exeDirectory, string appDataDirectory)
    {
        if (_probe.CanWrite(exeDirectory))
        {
            return exeDirectory;
        }

        _probe.EnsureExists(appDataDirectory);
        return appDataDirectory;
    }

    /// <summary>Full path for <paramref name="fileName"/> in the resolved base directory.</summary>
    public string ResolvePath(string exeDirectory, string appDataDirectory, string fileName)
        => Path.Combine(ResolveBaseDirectory(exeDirectory, appDataDirectory), fileName);
}
