using System.Net.Http;
using System.Text.Json;
using Langent.Core.Update;

namespace Langent.App.Services;

/// <summary>
/// Checks GitHub Releases at most once per day and reports a newer version
/// (SPEC §10). Never replaces the exe; failures are swallowed and retried next run.
/// </summary>
public sealed class UpdateChecker
{
    public const string Repository = "derpfeiffer-beep/langent";

    private readonly HttpClient _http;
    private readonly Logging _log;
    private readonly Version _current;
    private readonly Func<DateTime?> _getLastCheck;
    private readonly Action<DateTime> _setLastCheck;
    private readonly Action<string, string> _onUpdateAvailable;

    public UpdateChecker(
        HttpClient http,
        Logging log,
        Version current,
        Func<DateTime?> getLastCheck,
        Action<DateTime> setLastCheck,
        Action<string, string> onUpdateAvailable)
    {
        _http = http;
        _log = log;
        _current = current;
        _getLastCheck = getLastCheck;
        _setLastCheck = setLastCheck;
        _onUpdateAvailable = onUpdateAvailable;
    }

    public async Task CheckAsync(CancellationToken ct)
    {
        try
        {
            if (!UpdateCheck.ShouldCheckToday(_getLastCheck(), DateTime.Now))
            {
                return;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
            request.Headers.UserAgent.ParseAdd("Langent-UpdateCheck");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            _setLastCheck(DateTime.Now);

            if (!response.IsSuccessStatusCode)
            {
                _log.Info($"Update-Prüfung: HTTP {(int)response.StatusCode}.");
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;

            if (tag is not null && url is not null && UpdateCheck.IsNewer(tag, _current))
            {
                _log.Info($"Update verfügbar: {tag}.");
                _onUpdateAvailable(tag, url);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown or timeout — try again next start.
        }
        catch (Exception ex)
        {
            _log.Error("Update-Prüfung fehlgeschlagen", ex);
        }
    }
}
