using System.Text;
using System.Text.RegularExpressions;

namespace Langent.Core.Translation;

/// <summary>
/// Splits the model output into the detected source language (first line) and the
/// translation (SPEC §5.4). Works incrementally: push text as it streams, call
/// <see cref="Finish"/> at the end.
/// </summary>
/// <remarks>
/// The first line is the ISO code only if it matches <c>^[a-z]{2}$</c>; otherwise the
/// language is unknown and the whole output is translation. Leading and trailing blank
/// lines of the translation are removed (trailing whitespace is held back until more
/// text arrives, then dropped at <see cref="Finish"/>).
/// </remarks>
public sealed partial class LanguageLineParser
{
    [GeneratedRegex("^[a-z]{2}$")]
    private static partial Regex IsoCodeRegex();

    private readonly StringBuilder _firstLineBuffer = new();
    private bool _languageResolved;

    private bool _translationStarted;
    private string _heldWhitespace = "";

    public IReadOnlyList<TranslationUpdate> Push(string text)
    {
        var results = new List<TranslationUpdate>();
        if (text.Length == 0)
        {
            return results;
        }

        if (!_languageResolved)
        {
            _firstLineBuffer.Append(text);
            var buffered = _firstLineBuffer.ToString();
            var newlineIndex = buffered.IndexOf('\n');
            if (newlineIndex < 0)
            {
                return results; // still waiting for the first line to complete
            }

            var firstLine = TrimCr(buffered[..newlineIndex]);
            ResolveLanguage(firstLine, remainderIfKnown: buffered[(newlineIndex + 1)..], wholeIfUnknown: buffered, results);
        }
        else
        {
            EmitTranslation(text, results);
        }

        return results;
    }

    public IReadOnlyList<TranslationUpdate> Finish()
    {
        var results = new List<TranslationUpdate>();

        if (!_languageResolved)
        {
            // No newline ever arrived: same rule applied to the whole buffer.
            var buffered = _firstLineBuffer.ToString();
            ResolveLanguage(TrimCr(buffered), remainderIfKnown: "", wholeIfUnknown: buffered, results);
        }

        // Trailing whitespace held back is dropped.
        _heldWhitespace = "";
        return results;
    }

    private void ResolveLanguage(string firstLine, string remainderIfKnown, string wholeIfUnknown, List<TranslationUpdate> results)
    {
        _languageResolved = true;
        _firstLineBuffer.Clear();

        if (IsoCodeRegex().IsMatch(firstLine))
        {
            results.Add(new TranslationUpdate.SourceLanguageDetected(firstLine));
            EmitTranslation(remainderIfKnown, results);
        }
        else
        {
            results.Add(new TranslationUpdate.SourceLanguageDetected(null));
            EmitTranslation(wholeIfUnknown, results);
        }
    }

    private void EmitTranslation(string text, List<TranslationUpdate> results)
    {
        var pending = _heldWhitespace + text;
        _heldWhitespace = "";

        if (pending.Length == 0)
        {
            return;
        }

        if (!_translationStarted)
        {
            var firstNonWs = FirstNonWhitespace(pending);
            if (firstNonWs < 0)
            {
                _heldWhitespace = pending; // all whitespace so far: keep buffering (leading blanks)
                return;
            }

            pending = pending[firstNonWs..];
            _translationStarted = true;
        }

        var lastNonWs = LastNonWhitespace(pending);
        var toEmit = pending[..(lastNonWs + 1)];
        _heldWhitespace = pending[(lastNonWs + 1)..];

        if (toEmit.Length > 0)
        {
            results.Add(new TranslationUpdate.TextDelta(toEmit));
        }
    }

    private static string TrimCr(string s) => s.EndsWith('\r') ? s[..^1] : s;

    private static int FirstNonWhitespace(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (!char.IsWhiteSpace(s[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastNonWhitespace(string s)
    {
        for (var i = s.Length - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(s[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
