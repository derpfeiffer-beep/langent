using System.Text;

namespace Langent.Core.Infrastructure;

/// <summary>Default <see cref="IFileSystem"/> backed by <see cref="System.IO.File"/>, UTF-8 without BOM.</summary>
public sealed class FileSystem : IFileSystem
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

    public void WriteAllText(string path, string content) => File.WriteAllText(path, content, Utf8NoBom);

    public void AppendAllText(string path, string content) => File.AppendAllText(path, content, Utf8NoBom);
}
