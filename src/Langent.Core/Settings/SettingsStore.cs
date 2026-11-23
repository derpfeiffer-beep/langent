using System.Text.Json;
using System.Text.Json.Serialization;
using Langent.Core.Infrastructure;

namespace Langent.Core.Settings;

/// <summary>
/// Loads and saves <c>settings.json</c> (SPEC §6). The file lives next to the exe
/// when writable, otherwise under <c>%APPDATA%\Langent</c> (via <see cref="FilePathResolver"/>).
/// Missing fields fall back to defaults, unknown fields are ignored, a corrupt file
/// yields defaults, and <see cref="AppSettings.SchemaVersion"/> is preserved for future
/// migrations.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IFileSystem _fileSystem;

    public SettingsStore(IFileSystem fileSystem, FilePathResolver resolver, string exeDirectory, string appDataDirectory)
    {
        _fileSystem = fileSystem;
        SettingsFilePath = resolver.ResolvePath(exeDirectory, appDataDirectory, FileName);
    }

    /// <summary>Resolved absolute path of <c>settings.json</c>.</summary>
    public string SettingsFilePath { get; }

    public AppSettings Load()
    {
        if (!_fileSystem.FileExists(SettingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = _fileSystem.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, Options);
        _fileSystem.WriteAllText(SettingsFilePath, json);
    }
}
