using Langent.Core.Infrastructure;
using Langent.Core.Settings;
using Langent.Core.Tests.TestDoubles;
using Langent.Core.Translation;
using Xunit;

namespace Langent.Core.Tests.Settings;

public sealed class SettingsStoreTests
{
    private const string ExeDir = @"C:\Apps\Langent";
    private const string AppDataDir = @"C:\Users\dev\AppData\Roaming\Langent";

    private static SettingsStore Create(InMemoryFileSystem fs, params string[] writableDirs)
    {
        var resolver = new FilePathResolver(new FakeDirectoryProbe(writableDirs));
        return new SettingsStore(fs, resolver, ExeDir, AppDataDir);
    }

    [Fact]
    public void Missing_fields_fall_back_to_defaults()
    {
        var fs = new InMemoryFileSystem();
        var store = Create(fs, ExeDir);
        fs.Files[store.SettingsFilePath] = "{\"targetLanguage\":\"fr\"}";

        var settings = store.Load();

        Assert.Equal("fr", settings.TargetLanguage);
        Assert.Equal("en", settings.SecondaryLanguage);
        Assert.Equal("claude-haiku-5-5", settings.Model);
        Assert.Equal(400, settings.Trigger.WindowMs);
        Assert.Equal(2, settings.Prices.Count);
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        var fs = new InMemoryFileSystem();
        var store = Create(fs, ExeDir);
        fs.Files[store.SettingsFilePath] = "{\"targetLanguage\":\"fr\",\"somethingUnknown\":123}";

        var settings = store.Load();

        Assert.Equal("fr", settings.TargetLanguage);
    }

    [Fact]
    public void Corrupt_file_yields_defaults()
    {
        var fs = new InMemoryFileSystem();
        var store = Create(fs, ExeDir);
        fs.Files[store.SettingsFilePath] = "{ this is not valid json";

        var settings = store.Load();

        Assert.Equal("de", settings.TargetLanguage);
    }

    [Fact]
    public void Non_writable_exe_dir_uses_appdata_path()
    {
        var fs = new InMemoryFileSystem();
        var store = Create(fs, AppDataDir); // exe dir NOT writable

        Assert.StartsWith(AppDataDir, store.SettingsFilePath);
    }

    [Fact]
    public void Writable_exe_dir_uses_exe_path()
    {
        var fs = new InMemoryFileSystem();
        var store = Create(fs, ExeDir);

        Assert.StartsWith(ExeDir, store.SettingsFilePath);
    }

    [Fact]
    public void Save_then_load_round_trips_and_serialises_tone_as_string()
    {
        var fs = new InMemoryFileSystem();
        var store = Create(fs, ExeDir);

        store.Save(new AppSettings { TargetLanguage = "fr", Tone = Tone.Formal });

        Assert.Contains("\"formal\"", fs.Files[store.SettingsFilePath]);
        var reloaded = store.Load();
        Assert.Equal("fr", reloaded.TargetLanguage);
        Assert.Equal(Tone.Formal, reloaded.Tone);
    }
}
