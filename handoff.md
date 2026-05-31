# Canvas PDF Migration Handoff

Stand: 2026-05-31, Europe/Berlin

## Ziel

Weiterarbeit am Feature `Canvas.Migration.*`, das C#-PDF-Code aus Drittanbieter-Bibliotheken nach `Canvas.Pdf` migriert.

Der bisherige Fokus war:

- Allgemeine Architektur- und Provider-Checklisten
- Roslyn-basierte Migration-Piloten pro Anbieter
- WebApi-Konverter + UI-Beispiele für Migrationen
- Provider-Tests und API-Smoke-Tests

## Wichtige Arbeitsregeln

- Bestehende User-Änderungen nicht zurücksetzen.
- Besonders vorsichtig mit `bin/` und `obj/`: Viele Build-/Restore-Kommandos ändern getrackte Artefakte in diesem Repo.
- Vor finaler Übergabe möglichst:
  - `git status --short`
  - `git diff --check`
  - Build/Test nur, wenn Sandbox/Freigabe es zulässt.
- `dotnet test` scheitert in der Sandbox oft mit `System.Net.Sockets.SocketException (13): Permission denied`.
  - Dafür normalerweise `sandbox_permissions=require_escalated` verwenden.
  - Wenn Usage-Limit greift, Tests nicht per Workaround ausführen.
- `dotnet restore` kann auf macOS in der Sandbox mit `CSSM_ModuleLoad()` scheitern.
  - Dann mit Eskalation wiederholen, wenn verfügbar.

## Achtung: Aktueller Working Tree

Beim Erstellen dieses Handoffs zeigt `git status --short` unter anderem Änderungen, die nicht aus der Migration-Arbeit stammen bzw. nicht zurückgesetzt werden sollen:

- `Canvas.WebApi/Controllers/DocumentOpsController.cs`
- `src/Canvas.Infrastructure.Converters/Canvas.Infrastructure.Converters.csproj`
- Gelöschte Dateien:
  - `src/Canvas.Infrastructure.Converters/PdfImporter.cs`
  - `src/Canvas.Infrastructure.Converters/SvgPdfImporter.cs`
- UI/Docs/Export-Dateien:
  - `ui-designer-v2/src/hooks/useTemplateLoader.ts`
  - `ui-designer-v2/src/pages/DocsPage.tsx`
  - `ui-designer-v2/src/pages/IndexPage.tsx`
  - `ui-designer-v2/src/pages/TemplatePage.tsx`
  - `ui-designer-v2/src/services/ExportService.ts`
- Untracked:
  - `Removing-PDFImporter.md`
- Viele getrackte `obj/`-Artefakte in `samples/` und `tests/`.

Diese Änderungen im nächsten Chat erst prüfen, nicht blind revertieren.

## Migration-Provider Status

Alle ursprünglich geplanten Anbieter sind inzwischen mindestens als vorsichtiger Pilot umgesetzt.

Provider mit Roslyn-/Pattern-Pilot:

- `Canvas.Migration.SyncfusionPdf`
- `Canvas.Migration.iText7`
- `Canvas.Migration.AsposePdf`
- `Canvas.Migration.IronPdf`
- `Canvas.Migration.DevExpressPdf`
- `Canvas.Migration.Apryse`
- `Canvas.Migration.FoxitPdf`
- `Canvas.Migration.DsPdf`
- `Canvas.Migration.GemBoxPdf`
- `Canvas.Migration.SpirePdf`
- `Canvas.Migration.PdfKitNet`
- `Canvas.Migration.LeadtoolsPdf`
- `Canvas.Migration.ActivePdf`

Aktuell relevante neue/letzte Provider-Dateien:

- `src/Canvas.Migration.PdfKitNet/PdfKitNetMigration.cs`
- `tests/Canvas.Migration.PdfKitNet.Tests/PdfKitNetMigrationTests.cs`
- `src/Canvas.Migration.LeadtoolsPdf/LeadtoolsPdfMigration.cs`
- `tests/Canvas.Migration.LeadtoolsPdf.Tests/LeadtoolsPdfMigrationTests.cs`
- `src/Canvas.Migration.ActivePdf/ActivePdfMigration.cs`
- `tests/Canvas.Migration.ActivePdf.Tests/ActivePdfMigrationTests.cs`

WebApi-Konverter:

- `Canvas.WebApi/Services/Converters/PdfKitNetConverter.cs`
- `Canvas.WebApi/Services/Converters/LeadtoolsPdfConverter.cs`
- `Canvas.WebApi/Services/Converters/ActivePdfConverter.cs`

API-Smoke-Tests:

- `tests/Canvas.Api.Tests/MigrationServiceTests.cs`

UI:

- `ui-designer-v2/src/pages/MigrationsPage.tsx`

Checklisten:

- `checklists/Code-Migrations.md`
- `checklists/Code-Migrations-UI.md`
- `checklists/Code-Migration-PdfKitNet.md`
- `checklists/Code-Migration-LeadtoolsPdf.md`
- `checklists/Code-Migration-ActivePdf.md`

## Letzte Provider-Details

### PDFKit.NET

Status: `pilot cautious`

Wichtig:

- Package/API-Identität ist nicht bestätigt.
- Converter gibt immer `CANMIGPDFKIT000` als Warnung aus.
- Unterstützt einfache wahrscheinliche Patterns:
  - `new Document()` / `new PdfDocument()` / `new PDFDocument()`
  - `NewPage()` / `AddPage()` / `Pages.Add()`
  - `DrawText(...)` / `DrawString(...)`
  - `DrawLine(...)` / `DrawRectangle(...)`
  - `Save(...)` / `Render(...)` / `Write(...)` / `SaveAs(...)`
- Warnt bei Bildern, Forms, Security, Signaturen, Tabellen/Templates, bestehender PDF-Bearbeitung.

Verifikation damals:

- Provider build: grün
- Provider tests: 6/6 grün
- API tests nach PDFKit: 25/25 grün

### LEADTOOLS PDF

Status: `pilot cautious`

Wichtig:

- LEADTOOLS ist stark Raster/OCR/Conversion-lastig.
- Converter gibt immer `CANMIGLEAD000` als Warnung aus.
- Unterstützt nur wahrscheinliche direkte PDF-Erzeugung:
  - `PDFDocument` / `PdfDocument` / `PDFFile` / `PdfFile`
  - `AddPage()` / `NewPage()` / `Pages.Add()`
  - `DrawText(...)` / `DrawString(...)` / `TextOut(...)` / `AddText(...)`
  - `DrawLine(...)` / `DrawRectangle(...)`
  - `Save(...)` / `SaveToFile(...)` / `Write(...)` / `Export(...)`
- Warnt bei OCR, Raster, Barcode, DocumentConverter/DocumentFactory, Security, bestehender PDF-Bearbeitung.

Verifikation damals:

- Provider build: grün
- Provider tests: 6/6 grün
- API tests nach LEADTOOLS: 26/26 grün

### ActivePDF

Status: `pilot cautious`

Wichtig:

- ActivePDF hat mehrere Produktlinien: Toolkit, DocConverter, WebGrabber, Server/COM/Printer-Workflows.
- Converter gibt immer `CANMIGACTIVE000` als Warnung aus.
- Unterstützt wahrscheinliche Toolkit-artige Erzeugung:
  - `new Toolkit()` / `new APDoc()` / `new Document()`
  - `AddPage()` / `BeginPage()` / `NewPage()`
  - `PrintText(...)` / `DrawText(...)` / `AddText(...)` / `TextOut(...)`
  - `DrawLine(...)` / `DrawRectangle(...)`
  - `Save(...)` / `SaveAs(...)` / `SaveToFile(...)` / `CloseDocument(...)`
- Warnt bei DocConverter/WebGrabber, COM/server automation, printer output, merge/stamp, HTML conversion, existing PDF editing, security/signatures.

Verifikation:

- ActivePDF provider build: grün, 0 Fehler
- API build: grün, 0 Fehler
- `dotnet test` konnte zuletzt nicht mit Eskalation laufen, weil Usage-Limit die Freigabe blockiert hat.
- Sandbox-`dotnet test` scheiterte erwartungsgemäß an VSTest Socket Permission.

## Aktueller Nächster Schritt

Empfohlene nächste Reihenfolge:

1. Working Tree bereinigen bzw. bewusst trennen:
   - Prüfen, welche Änderungen wirklich Migration sind.
   - Build-Artefakte unter `obj/`/`bin/` vorsichtig zurücksetzen, falls sie nur generiert sind.
   - Nicht die PDF-Importer-Entfernungen oder UI/Docs-Dateien anfassen, bevor klar ist, ob sie vom User gewollt sind.
2. Sobald Freigabe/Limit wieder verfügbar:
   - `dotnet test tests/Canvas.Migration.ActivePdf.Tests/Canvas.Migration.ActivePdf.Tests.csproj --no-restore --no-build -nodeReuse:false`
   - `dotnet test tests/Canvas.Api.Tests/Canvas.Api.Tests.csproj --no-restore --no-build -nodeReuse:false`
3. Wenn Tests grün:
   - Checklisten final prüfen.
   - `git diff --check` auf Quelländerungen und idealerweise gesamtem Diff nach Artefakt-Cleanup.
4. Danach mögliche Architektur-Aufräumarbeiten:
   - Gemeinsame Helper für wiederkehrende Migration-Muster extrahieren.
   - Diagnostics-Konventionen vereinheitlichen.
   - Provider-neutralen Mapping-Report ausbauen.
   - Real-World-Samples je Anbieter sammeln und Snapshot-Tests ergänzen.

## Wichtige Kommandos

Provider-/API-Builds:

```bash
dotnet build tests/Canvas.Migration.ActivePdf.Tests/Canvas.Migration.ActivePdf.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1 -nodeReuse:false
dotnet build tests/Canvas.Api.Tests/Canvas.Api.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1 -nodeReuse:false
```

Tests, wenn Eskalation möglich:

```bash
dotnet test tests/Canvas.Migration.ActivePdf.Tests/Canvas.Migration.ActivePdf.Tests.csproj --no-restore --no-build -nodeReuse:false
dotnet test tests/Canvas.Api.Tests/Canvas.Api.Tests.csproj --no-restore --no-build -nodeReuse:false
```

Status/Checks:

```bash
git status --short
git diff --check
```

## Hinweis für den nächsten Chat

Der Kontext ist groß geworden. Bitte nicht von vorne anfangen. Erst lokale Dateien lesen, besonders:

- `checklists/Code-Migrations.md`
- `checklists/Code-Migrations-UI.md`
- `tests/Canvas.Api.Tests/MigrationServiceTests.cs`
- `Canvas.WebApi/Services/Converters/*PdfConverter.cs`
- die drei letzten Provider unter `src/Canvas.Migration.*`

Dann den aktuellen Working Tree sauber einordnen und erst danach weiterarbeiten.
