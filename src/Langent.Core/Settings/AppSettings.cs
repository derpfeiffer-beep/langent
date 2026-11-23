using Langent.Core.Translation;
using Langent.Core.Trigger;

namespace Langent.Core.Settings;

/// <summary>Per-model price in USD per million tokens (SPEC §6/§7).</summary>
public sealed record ModelPrice(decimal InputPerMTokUsd, decimal OutputPerMTokUsd);

/// <summary>Trigger timing (SPEC §6 "trigger").</summary>
public sealed class TriggerSettings
{
    public bool DoubleCopyEnabled { get; set; } = true;
    public int WindowMs { get; set; } = 400;
    public int BurstMergeMs { get; set; } = 80;
}

/// <summary>Remembered window size (SPEC §6 "window").</summary>
public sealed class WindowSettings
{
    public int Width { get; set; } = 560;
    public int Height { get; set; } = 420;
}

/// <summary>
/// The persisted settings model (SPEC §6). Property initialisers are the default
/// values, so deserialising a partial <c>settings.json</c> leaves missing fields at
/// their defaults and ignores unknown fields.
/// </summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string TargetLanguage { get; set; } = "de";
    public string SecondaryLanguage { get; set; } = "en";
    public string Model { get; set; } = "claude-haiku-5-5";
    public string QualityModel { get; set; } = "claude-sonnet-5-5";
    public Tone Tone { get; set; } = Tone.Neutral;
    public string CustomInstructions { get; set; } = "";
    public IReadOnlyList<GlossaryEntry> Glossary { get; set; } = [];
    public TriggerSettings Trigger { get; set; } = new();
    public IReadOnlyList<string> ExcludedProcesses { get; set; } = [.. AppProfiles.DefaultExcludedProcesses];
    public bool RestoreClipboard { get; set; } = true;
    public int MaxInputChars { get; set; } = 10_000;
    public bool Autostart { get; set; } = false;
    public decimal? MonthlyBudgetUsd { get; set; }

    /// <summary>
    /// Prices per model. Defaults from the Claude API reference (cached 2026-11-03),
    /// first-party ≤100K-token tier — the user verifies these (SPEC §6).
    /// </summary>
    public IReadOnlyDictionary<string, ModelPrice> Prices { get; set; } = new Dictionary<string, ModelPrice>
    {
        ["claude-haiku-5-5"] = new ModelPrice(0.10m, 0.50m),
        ["claude-sonnet-5-5"] = new ModelPrice(2.00m, 10.00m),
    };

    public string? ApiKeyProtected { get; set; }
    public WindowSettings Window { get; set; } = new();
}
