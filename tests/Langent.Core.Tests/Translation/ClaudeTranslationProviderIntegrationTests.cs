using System.Text;
using Langent.Core.Translation;
using Xunit;

namespace Langent.Core.Tests.Translation;

/// <summary>
/// Live API test (SPEC M3). It only does real work when HOTKEYTRANSLATOR_API_KEY is
/// set; without it the test is a no-op so the suite stays green offline.
/// </summary>
public sealed class ClaudeTranslationProviderIntegrationTests
{
    [Fact]
    public async Task Translates_guten_morgen_to_english()
    {
        var key = Environment.GetEnvironmentVariable("HOTKEYTRANSLATOR_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            return; // no key: skip silently
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var provider = new ClaudeTranslationProvider(http, () => key);
        var request = new TranslationRequest(
            "Guten Morgen", "en", "de", null, "claude-haiku-5-5", Tone.Neutral, [], "");

        var text = new StringBuilder();
        TranslationUpdate.Completed? completed = null;

        await foreach (var update in provider.TranslateAsync(request, CancellationToken.None))
        {
            switch (update)
            {
                case TranslationUpdate.TextDelta d:
                    text.Append(d.Text);
                    break;
                case TranslationUpdate.Completed c:
                    completed = c;
                    break;
            }
        }

        Assert.NotEqual(0, text.Length);
        Assert.NotNull(completed);
        Assert.True(completed!.InputTokens > 0);
    }
}
