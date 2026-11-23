using Langent.Core.Translation;
using Xunit;

namespace Langent.Core.Tests.Translation;

public sealed class LanguageLineParserTests
{
    private static (bool HasLanguage, string? Iso, string Text) Run(params string[] chunks)
    {
        var parser = new LanguageLineParser();
        var updates = new List<TranslationUpdate>();
        foreach (var chunk in chunks)
        {
            updates.AddRange(parser.Push(chunk));
        }

        updates.AddRange(parser.Finish());

        var hasLanguage = false;
        string? iso = null;
        var text = "";
        foreach (var u in updates)
        {
            switch (u)
            {
                case TranslationUpdate.SourceLanguageDetected d:
                    hasLanguage = true;
                    iso = d.IsoCode;
                    break;
                case TranslationUpdate.TextDelta t:
                    text += t.Text;
                    break;
            }
        }

        return (hasLanguage, iso, text);
    }

    [Fact]
    public void Code_then_translation()
    {
        var (hasLang, iso, text) = Run("de\nHello");
        Assert.True(hasLang);
        Assert.Equal("de", iso);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void Newline_arrives_in_second_delta()
    {
        var (hasLang, iso, text) = Run("d", "e\nHel", "lo");
        Assert.True(hasLang);
        Assert.Equal("de", iso);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void First_line_not_a_code_means_unknown_and_full_text()
    {
        var (hasLang, iso, text) = Run("Hello\nWorld");
        Assert.True(hasLang);
        Assert.Null(iso);
        Assert.Equal("Hello\nWorld", text);
    }

    [Fact]
    public void No_newline_until_end()
    {
        var (hasLang, iso, text) = Run("Hello");
        Assert.True(hasLang);
        Assert.Null(iso);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void Uppercase_first_line_is_not_a_code()
    {
        var (hasLang, iso, text) = Run("EN\nHallo");
        Assert.True(hasLang);
        Assert.Null(iso);
        Assert.Equal("EN\nHallo", text);
    }

    [Fact]
    public void Leading_and_trailing_blank_lines_are_removed()
    {
        var (hasLang, iso, text) = Run("de\n\n\nHello\n\n");
        Assert.True(hasLang);
        Assert.Equal("de", iso);
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void Internal_line_breaks_are_preserved()
    {
        var (_, iso, text) = Run("de\nLine1\n\nLine2\n");
        Assert.Equal("de", iso);
        Assert.Equal("Line1\n\nLine2", text);
    }
}
