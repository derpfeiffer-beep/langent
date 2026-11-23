using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Langent.Core.Trigger;

namespace Langent.App.Services;

/// <summary>Saved clipboard formats for later restore (SPEC §4.2 step 3).</summary>
public sealed class ClipboardSnapshot
{
    public string? UnicodeText { get; init; }
    public string? Html { get; init; }
    public string? Rtf { get; init; }

    public bool IsEmpty => UnicodeText is null && Html is null && Rtf is null;
}

/// <summary>
/// Reads, saves, writes and restores the clipboard (SPEC §4.2/§4.3). All methods
/// must run on the STA UI thread. Writes set the detector suppression window
/// (500 ms) first and add <c>ExcludeClipboardContentFromMonitorProcessing</c> so
/// Windows clipboard history and cloud sync ignore the replacement.
/// </summary>
public sealed class ClipboardService
{
    private const string ExcludeFromHistoryFormat = "ExcludeClipboardContentFromMonitorProcessing";
    private const int Attempts = 5;
    private const int RetryDelayMs = 30;
    private const int SuppressWindowMs = 500;

    private readonly Stopwatch _clock;
    private readonly DoubleCopyDetector _detector;

    public ClipboardService(Stopwatch clock, DoubleCopyDetector detector)
    {
        _clock = clock;
        _detector = detector;
    }

    /// <summary>Reads plain Unicode text, retrying up to 5× while the clipboard is locked.</summary>
    public string? ReadUnicodeText()
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                return Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? Clipboard.GetText(TextDataFormat.UnicodeText)
                    : null;
            }
            catch (Exception) when (attempt < Attempts - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        return null;
    }

    /// <summary>Saves Unicode/HTML/RTF for later restore. Other formats are not saved.</summary>
    public ClipboardSnapshot? Save()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null)
            {
                return null;
            }

            return new ClipboardSnapshot
            {
                UnicodeText = data.GetDataPresent(DataFormats.UnicodeText)
                    ? data.GetData(DataFormats.UnicodeText) as string
                    : null,
                Html = data.GetDataPresent(DataFormats.Html)
                    ? data.GetData(DataFormats.Html) as string
                    : null,
                Rtf = data.GetDataPresent(DataFormats.Rtf)
                    ? data.GetData(DataFormats.Rtf) as string
                    : null,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Writes text with the exclude-from-history format and sets suppression.</summary>
    public void WriteText(string text)
    {
        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        data.SetData(ExcludeFromHistoryFormat, new MemoryStream());

        Suppress();
        SetDataObject(data);
    }

    /// <summary>Restores a previously saved snapshot (SPEC §4.3 step 6) and sets suppression.</summary>
    public void Restore(ClipboardSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.IsEmpty)
        {
            return;
        }

        var data = new DataObject();
        if (snapshot.UnicodeText is not null)
        {
            data.SetText(snapshot.UnicodeText, TextDataFormat.UnicodeText);
        }

        if (snapshot.Html is not null)
        {
            data.SetData(DataFormats.Html, snapshot.Html);
        }

        if (snapshot.Rtf is not null)
        {
            data.SetData(DataFormats.Rtf, snapshot.Rtf);
        }

        Suppress();
        SetDataObject(data);
    }

    private void Suppress() => _detector.Suppress(_clock.ElapsedMilliseconds + SuppressWindowMs);

    private static void SetDataObject(DataObject data)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return;
            }
            catch (Exception) when (attempt < Attempts - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }
    }
}
