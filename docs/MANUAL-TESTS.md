# Manuelle Tests (Windows)

Werden vom User nach dem jeweiligen Meilenstein ausgeführt. Ergebnis je Ziel-App eintragen: ok / Fehler (kurze Beschreibung).

Ziel-Apps: Editor (Notepad), Word, Outlook (Mail verfassen), Chrome oder Edge (Textfeld, z. B. Webmail), Teams (Chat-Eingabe), Windows Terminal (muss ignoriert werden).

## M1 – Auslöser und Ersetzen

| Nr. | Schritt | Erwartung | Notepad | Word | Outlook | Browser | Teams | Terminal |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| T1 | Ein Wort markieren, Strg+C+C | Wort wird durch Großbuchstaben ersetzt | | | | | | nichts passiert |
| T2 | Text markieren, nur einmal Strg+C | Nichts passiert, normales Kopieren funktioniert | | | | | | |
| T3 | Text markieren, Strg+C+C, danach Strg+V an anderer Stelle | Eingefügt wird der ursprünglich kopierte Text (Zwischenablage wiederhergestellt) | | | | | | |

Zusätzlich notieren: Wie schnell muss das zweite C kommen, damit es zuverlässig auslöst? Gibt es Fehlauslöser beim normalen Arbeiten?

## M4 – Übersetzungsfenster

| Nr. | Schritt | Erwartung |
| --- | --- | --- |
| T4 | Deutschen Satz markieren, Strg+C+C (Ziel Deutsch, Zweitsprache Englisch) | Fenster öffnet nahe dem Mauszeiger, englische Übersetzung erscheint während des Schreibens, Fußzeile zeigt de → en und Kosten |
| T5 | Übersetzung bearbeiten, Strg+Enter | Markierung im Ursprungsprogramm ist durch den bearbeiteten Text ersetzt, Fenster ist zu |
| T6 | Während der Übersetzung Zielsprache wechseln | Alte Anfrage bricht ab, neue Übersetzung startet |
| T7 | Strg+Shift+C, dann in anderem Programm Strg+V | Übersetzung wird eingefügt |
| T8 | WLAN aus, Strg+C+C | Meldung „Keine Verbindung zur Claude API.“ mit „Erneut versuchen“ |

## M5 – Einstellungen und Betrieb

| Nr. | Schritt | Erwartung |
| --- | --- | --- |
| T9 | `settings.json` löschen, Exe starten | Einstellungen öffnen mit Hinweis auf fehlenden Schlüssel; nach Eintragen klappt „Verbindung testen“ |
| T10 | Autostart an, abmelden, anmelden | App läuft im Tray |
| T11 | Tray-Tooltip ansehen nach drei Übersetzungen | Kosten heute/Monat passen zu `usage.csv` |
| T12 | Pausieren, Strg+C+C | Nichts passiert; nach erneutem Klick wieder aktiv |
