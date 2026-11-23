namespace Langent.Core.Infrastructure;

/// <summary>Minimal text-file IO abstraction so file-backed Core classes stay unit-testable.</summary>
public interface IFileSystem
{
    bool FileExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string content);

    void AppendAllText(string path, string content);
}
