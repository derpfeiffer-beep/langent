using Langent.Core.Translation;
using Xunit;

namespace Langent.Core.Tests.Translation;

public sealed class TranslationErrorTextTests
{
    [Theory]
    [InlineData(ErrorKind.MissingKey, "Kein API-Schlüssel hinterlegt. Bitte in den Einstellungen eintragen.")]
    [InlineData(ErrorKind.InvalidKey, "API-Schlüssel ungültig.")]
    [InlineData(ErrorKind.NoCredit, "API-Guthaben aufgebraucht.")]
    [InlineData(ErrorKind.RateLimited, "Zu viele Anfragen, bitte kurz warten.")]
    [InlineData(ErrorKind.Overloaded, "Claude ist gerade überlastet.")]
    [InlineData(ErrorKind.Network, "Keine Verbindung zur Claude API.")]
    public void Maps_each_kind_to_its_message(ErrorKind kind, string expected)
    {
        Assert.Equal(expected, TranslationErrorText.For(kind));
    }

    [Fact]
    public void Unknown_includes_status_code_when_present()
    {
        Assert.Equal("Übersetzung fehlgeschlagen (503).", TranslationErrorText.For(ErrorKind.Unknown, 503));
    }

    [Fact]
    public void Unknown_without_status_code()
    {
        Assert.Equal("Übersetzung fehlgeschlagen.", TranslationErrorText.For(ErrorKind.Unknown));
    }
}
