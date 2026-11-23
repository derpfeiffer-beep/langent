# HotkeyTranslator

Portable Windows-Tray-App: Text markieren, Strg+C+C, Claude übersetzt, Strg+Enter ersetzt die Markierung. Einziger Nutzer ist der Entwickler.

## Quelle der Wahrheit

- `docs/SPEC.md` ist verbindlich. Nichts bauen, was dort nicht steht. Lücken oder Widersprüche: nachfragen.
- `docs/MANUAL-TESTS.md` enthält die Tests, die nur auf Windows von Hand gehen.

## Arbeitsweise

- Immer genau einen Meilenstein aus `docs/SPEC.md` Abschnitt 11 umsetzen, dann stoppen.
- Vor dem Code: kurzen Plan zeigen (Dateien, Klassen, Tests) und auf Freigabe warten.
- Core-Logik testgetrieben: erst Tests aus Spezifikation, Abschnitt 12, dann Implementierung.
- Abschlussbericht je Meilenstein: was geändert wurde, Abweichungen von der Spezifikation mit Grund, welche manuellen Tests der User jetzt ausführen soll.
- Kleine, thematische Commits mit aussagekräftiger Nachricht. Nicht pushen ohne Auftrag.

## Befehle

```
dotnet build
dotnet test
dotnet publish src/HotkeyTranslator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Läuft Claude Code nicht auf Windows: Core und Tests bauen und testen trotzdem; für das App-Projekt `-p:EnableWindowsTargeting=true` setzen, Ausführen ist dann nicht möglich.

## Architekturregeln

- `HotkeyTranslator.Core` (net10.0) hat keine Windows-Abhängigkeit. Alles Testbare gehört dorthin: Zeitlogik, Parser, Prompt, Kosten, Einstellungen. Zeit und Dateisystem werden injiziert.
- `HotkeyTranslator.App` (net10.0-windows, WPF) bleibt dünn: P/Invoke nur in `Native/NativeMethods.cs`, Abläufe in `AppController`.
- Kein Tastatur-Hook. Strg+C+C wird über `AddClipboardFormatListener` erkannt.
- Claude API direkt über `HttpClient` mit Streaming, kein SDK. Vor der Umsetzung die aktuelle Doku unter https://docs.claude.com prüfen.
- Erlaubte NuGet-Pakete: `H.NotifyIcon.Wpf`, `CommunityToolkit.Mvvm`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`. Andere nur nach Rückfrage.

## Code-Stil

- Nullable an, Warnungen sind Fehler, file-scoped namespaces, `async` durchgehend mit `CancellationToken`.
- Oberflächentexte auf Deutsch, Code und Bezeichner auf Englisch.

## Sicherheit

- Niemals übersetzte Texte, Originaltexte oder API-Schlüssel loggen oder in Tests/Fixtures einchecken.
- API-Schlüssel für Entwicklung nur über die Umgebungsvariable `HOTKEYTRANSLATOR_API_KEY` (bewusst nicht `ANTHROPIC_API_KEY`, weil Claude Code diese Variable selbst auswertet). `settings.json`, `usage.csv`, `log.txt` und `publish/` stehen in `.gitignore`.

## Fertig heißt

- `dotnet build` ohne Warnungen, `dotnet test` grün.
- Abnahmekriterium des Meilensteins aus Spezifikation, Abschnitt 11 erfüllt oder Abweichung begründet.
- `docs/MANUAL-TESTS.md` aktualisiert, falls sich Abläufe geändert haben.
