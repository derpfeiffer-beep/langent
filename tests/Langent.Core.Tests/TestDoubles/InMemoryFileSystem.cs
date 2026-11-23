using Langent.Core.Infrastructure;

namespace Langent.Core.Tests.TestDoubles;

public sealed class InMemoryFileSystem : IFileSystem
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => Files.ContainsKey(path);

    public string ReadAllText(string path) => Files[path];

    public void WriteAllText(string path, string content) => Files[path] = content;

    public void AppendAllText(string path, string content)
        => Files[path] = (Files.TryGetValue(path, out var existing) ? existing : "") + content;
}
