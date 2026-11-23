using System.IO;
using Langent.Core.Infrastructure;

namespace Langent.App.Infrastructure;

/// <summary>
/// Resolves the directory and file paths used at runtime, following SPEC §6:
/// next to the exe when writable, otherwise <c>%APPDATA%\Langent\</c>.
/// </summary>
public sealed class AppPaths
{
    public const string AppDataFolderName = "Langent";

    public string BaseDirectory { get; }
    public string LogFilePath { get; }

    private AppPaths(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        LogFilePath = Path.Combine(baseDirectory, "log.txt");
    }

    public static AppPaths Resolve()
    {
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolderName);

        var resolver = new FilePathResolver(new FileSystemDirectoryProbe());
        var baseDirectory = resolver.ResolveBaseDirectory(GetExeDirectory(), appDataDirectory);
        return new AppPaths(baseDirectory);
    }

    /// <summary>
    /// The directory holding the running exe. For a single-file self-extracting build
    /// <see cref="AppContext.BaseDirectory"/> is the temp extraction folder, so when the
    /// process is our own exe we use <see cref="Environment.ProcessPath"/> instead; in a
    /// dev run via <c>dotnet App.dll</c> we fall back to the build output directory.
    /// </summary>
    public static string GetExeDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (processPath is not null
            && string.Equals(Path.GetFileNameWithoutExtension(processPath), "Langent", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        }

        return AppContext.BaseDirectory;
    }
}
