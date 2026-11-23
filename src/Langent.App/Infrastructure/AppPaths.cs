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
        var exeDirectory = AppContext.BaseDirectory;
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolderName);

        var resolver = new FilePathResolver(new FileSystemDirectoryProbe());
        var baseDirectory = resolver.ResolveBaseDirectory(exeDirectory, appDataDirectory);
        return new AppPaths(baseDirectory);
    }
}
