using Langent.Core.Infrastructure;
using Xunit;

namespace Langent.Core.Tests.Infrastructure;

public sealed class FilePathResolverTests
{
    private sealed class FakeProbe : IDirectoryProbe
    {
        private readonly HashSet<string> _writable;
        public List<string> Ensured { get; } = [];

        public FakeProbe(params string[] writable) => _writable = [.. writable];

        public bool CanWrite(string directory) => _writable.Contains(directory);

        public void EnsureExists(string directory) => Ensured.Add(directory);
    }

    private const string ExeDir = @"C:\Apps\Langent";
    private const string AppDataDir = @"C:\Users\dev\AppData\Roaming\Langent";

    [Fact]
    public void Uses_exe_directory_when_writable()
    {
        var probe = new FakeProbe(ExeDir);
        var resolver = new FilePathResolver(probe);

        var result = resolver.ResolveBaseDirectory(ExeDir, AppDataDir);

        Assert.Equal(ExeDir, result);
        Assert.Empty(probe.Ensured);
    }

    [Fact]
    public void Falls_back_to_appdata_when_exe_directory_not_writable()
    {
        var probe = new FakeProbe(AppDataDir);
        var resolver = new FilePathResolver(probe);

        var result = resolver.ResolveBaseDirectory(ExeDir, AppDataDir);

        Assert.Equal(AppDataDir, result);
        Assert.Contains(AppDataDir, probe.Ensured);
    }

    [Fact]
    public void ResolvePath_combines_base_directory_with_file_name()
    {
        var probe = new FakeProbe(ExeDir);
        var resolver = new FilePathResolver(probe);

        var result = resolver.ResolvePath(ExeDir, AppDataDir, "log.txt");

        Assert.Equal(Path.Combine(ExeDir, "log.txt"), result);
    }
}
