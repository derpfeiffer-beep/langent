using Langent.Core.Settings;

namespace Langent.App.Services;

/// <summary>
/// Resolves the Claude API key (SPEC §6): the environment variable
/// <c>HOTKEYTRANSLATOR_API_KEY</c> takes precedence (development), otherwise the
/// DPAPI-protected key from settings. Returns null when neither yields a key.
/// </summary>
public sealed class ApiKeyResolver
{
    public const string EnvironmentVariable = "HOTKEYTRANSLATOR_API_KEY";

    private readonly Func<AppSettings> _settings;

    public ApiKeyResolver(Func<AppSettings> settings) => _settings = settings;

    public string? Resolve()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        return SecretProtector.Unprotect(_settings().ApiKeyProtected);
    }
}
