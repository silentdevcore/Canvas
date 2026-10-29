# PXA PDF Migration Handoff

> Naming history: This retained planning or migration note predates PVAD/PVADE. Legacy project names below are historical references, not current integration instructions. See README.md and the rename compatibility guides for the current contracts.

Stand: 2026-05-31, Europe/Berlin

## Ziel

Weiterarbeit am Feature `PVAD.Migration.*`, das C#-PDF-Code aus Drittanbieter-Bibliotheken nach `PVAD.Pdf` migriert.

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

- `PVAD.WebApi/Controllers/DocumentOpsController.cs`
- `src/Infrastructure/PVAD.Infrastructure.Converters/PVAD.Infrastructure.Converters.csproj`
- Gelöschte Dateien:
  - `src/Infrastructure/PVAD.Infrastructure.Converters/PdfImporter.cs`
  - `src/Infrastructure/PVAD.Infrastructure.Converters/SvgPdfImporter.cs`
- UI/Docs/Export-Dateien:
  - `pvad-designer/src/hooks/useTemplateLoader.ts`
  - `pvad-designer/src/pages/DocsPage.tsx`
  - `pvad-designer/src/pages/IndexPage.tsx`
  - `pvad-designer/src/pages/TemplatePage.tsx`
  - `pvad-designer/src/services/ExportService.ts`
- Untracked:
  - `Removing-PDFImporter.md`
- Viele getrackte `obj/`-Artefakte in `samples/` und `tests/`.

Diese Änderungen im nächsten Chat erst prüfen, nicht blind revertieren.

## Migration-Provider Status

Alle ursprünglich geplanten Anbieter sind inzwischen mindestens als vorsichtiger Pilot umgesetzt.

Provider mit Roslyn-/Pattern-Pilot:

- `PVAD.Migration.Pdf.Code.Syncfusion`
- `PVAD.Migration.Pdf.Code.IText7`
- `PVAD.Migration.Pdf.Code.Aspose`
- `PVAD.Migration.Pdf.Code.IronPdf`
- `PVAD.Migration.Pdf.Code.DevExpress`
- `PVAD.Migration.Pdf.Code.Apryse`
- `PVAD.Migration.Pdf.Code.Foxit`
- `PVAD.Migration.Pdf.Code.DsPdf`
- `PVAD.Migration.Pdf.Code.GemBox`
- `PVAD.Migration.Pdf.Code.Spire`
- `PVAD.Migration.Pdf.Code.PdfKitNet`
- `PVAD.Migration.Pdf.Code.Leadtools`
- `PVAD.Migration.Pdf.Code.ActivePdf`
- `PVAD.Migration.Pdf.Code.PdfTools`
- `PVAD.Migration.Pdf.Code.PdfToolsToolbox`

Aktuell relevante neue/letzte Provider-Dateien:

- `src/Migrations/PDF/PVAD.Migration.Pdf.Code.PdfKitNet/PdfKitNetMigration.cs`
- `tests/PVAD.Migration.Pdf.Code.PdfKitNet.Tests/PdfKitNetMigrationTests.cs`
- `src/Migrations/PDF/PVAD.Migration.Pdf.Code.Leadtools/LeadtoolsPdfMigration.cs`
- `tests/PVAD.Migration.Pdf.Code.Leadtools.Tests/LeadtoolsPdfMigrationTests.cs`
- `src/Migrations/PDF/PVAD.Migration.Pdf.Code.ActivePdf/ActivePdfMigration.cs`
- `tests/PVAD.Migration.Pdf.Code.ActivePdf.Tests/ActivePdfMigrationTests.cs`
- `src/Migrations/PDF/PVAD.Migration.Pdf.Code.PdfTools/PdfToolsMigration.cs`
- `tests/PVAD.Migration.Pdf.Code.PdfTools.Tests/PdfToolsMigrationTests.cs`
- `src/Migrations/PDF/PVAD.Migration.Pdf.Code.PdfToolsToolbox/PdfToolsToolboxMigration.cs`
- `tests/PVAD.Migration.Pdf.Code.PdfToolsToolbox.Tests/PdfToolsToolboxMigrationTests.cs`

WebApi-Konverter:

- `PVAD.WebApi/Services/Converters/PdfKitNetConverter.cs`
- `PVAD.WebApi/Services/Converters/LeadtoolsPdfConverter.cs`
- `PVAD.WebApi/Services/Converters/ActivePdfConverter.cs`
- `PVAD.WebApi/Services/Converters/PdfToolsConverter.cs`
- `PVAD.WebApi/Services/Converters/PdfToolsToolboxConverter.cs`

API-Smoke-Tests:

- `tests/PVAD.Api.Tests/MigrationServiceTests.cs`

UI:

- `pvad-designer/src/pages/MigrationsPage.tsx`

Checklisten:

- `checklists/Code-Migrations.md`
- `checklists/Code-Migrations-UI.md`
- `checklists/Code-Migration-PdfKitNet.md`
- `checklists/Code-Migration-LeadtoolsPdf.md`
- `checklists/Code-Migration-ActivePdf.md`
- `checklists/Code-Migration-PdfTools.md`
- `checklists/Code-Migration-PdfToolsToolbox.md`

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
- ActivePDF provider tests: 6/6 grün
- API tests: 28/28 grün
- Sandbox-`dotnet test` scheitert weiterhin erwartungsgemäß an VSTest Socket Permission; mit Eskalation laufen die Tests.

### PDFTools / Pdftools SDK

Status: `pilot cautious`

Wichtig:

- Offizielle .NET-Doku bestätigt NuGet `PdfTools`, Docs-Version 1.17 und optionales `Sdk.Initialize(...)` für lizenzierte Ausgabe.
- Converter gibt immer `CANMIGPDFTOOLS000` als diagnostics-first-pilot-Warnung aus.
- `Sdk.Initialize(...)` wird entfernt und mit `CANMIGPDFTOOLS001` dokumentiert.
- Offizielle API-Referenzen zeigen: `PdfTools.Pdf.Document` wird geöffnet oder durch Operationen erzeugt; direkte PDF-Erzeugung liegt im separaten PDF Toolbox SDK/add-on (`PdfTools.Toolbox.Pdf.Document.Create`, `Page.Create`).
- Keine automatischen `Document/AddPage/DrawText`-PXA-Rewrites mehr für diesen SDK-Provider.
- Warnt bei Document.Open/Save, Conversion, Rendering/Image, Optimization, Validation/Repair, existing PDF processing, Security, Signaturen, Forms, Annotationen, Outlines/OCR.
- Warnt mit `CANMIGPDFTOOLS022`, wenn `PdfTools.Toolbox.*` erkannt wird, damit Toolbox als eigener Sample-/Provider-Schnitt behandelt wird.

Verifikation:

- PDFTools provider build: grün, 0 Fehler
- PDFTools provider tests: 5/5 grün
- API build: grün, 0 Fehler; bestehende Warnungen in Core/Converters/Apryse/WebApi
- API tests: 28/28 grün

### PDF Toolbox SDK / Toolbox add-on

Status: `pilot cautious`

Wichtig:

- Separat vom Pdftools SDK behandeln.
- Offizielle Doku bestätigt die Direct-Generation-/Content-Spur:
  - `PdfTools.Toolbox.Pdf.Document.Create(...)`
  - `PdfTools.Toolbox.Pdf.Page.Create(...)`
  - `ContentGenerator`
  - `Text.Create(...)`
  - `TextGenerator`
  - `Font.CreateFromSystem(...)`
- NuGet/Assembly verifiziert:
  - NuGet: `PdfTools.Toolbox` 1.11.0
  - Assembly: `PdfTools.Toolbox.dll`
  - Namespace-Familie: `PdfTools.Toolbox.*`
- Toolbox add-on braucht laut Getting-Started eigene Lizenz/Trial-Behandlung; das ist anders als Pdftools SDK.
- Unterstützt im ersten Pilot:
  - `Document.Create(outStream, ...)` -> `new PdfDocument()`
  - `Page.Create(outDoc, PageSize.A4/Letter)` -> `document.AddPage(PdfPagePreset.A4/Letter, landscape)`
  - `Page.Create(outDoc, customSize)` -> `document.AddPage()` plus `CANMIGPDFTOOLBOX009`
  - `TextGenerator.MoveTo(...)` + `ShowLine(...)` -> `page.DrawTextFromTop(...)`
  - `outDoc.Pages.Add(outPage)` wird entfernt, wenn PageCreate bereits migriert wurde
  - einfache `FileStream(outPath, ...)` / `File.Create(outPath)` Output-Ziele werden erkannt und zu `document.Save(outPath)` migriert
- Warnt mit `CANMIGPDFTOOLBOX008`, wenn ein Output-Stream nicht sicher zu einem Pfad gemappt werden kann.
- Warnt mit `CANMIGPDFTOOLBOX010`, wenn nach teilweiser Migration Toolbox-Code übrig bleibt; Toolbox-Usings bleiben dann erhalten.
- Warnt bei Existing-PDF Copy/Edit/Tagging, Forms, Annotations, Metadata, Outlines, Color/Paint/Transparency/Image/Barcode/Watermark Details.

Verifikation:

- PDF Toolbox provider build: grün, 0 Fehler; NuGet-Audit-Warnung wegen eingeschränktem Netzwerk
- PDF Toolbox provider tests: 9/9 grün

## Aktueller Nächster Schritt

Empfohlene nächste Reihenfolge:

1. Working Tree bereinigen bzw. bewusst trennen:
   - Prüfen, welche Änderungen wirklich Migration sind.
   - Build-Artefakte unter `obj/`/`bin/` vorsichtig zurücksetzen, falls sie nur generiert sind.
   - Nicht die PDF-Importer-Entfernungen oder UI/Docs-Dateien anfassen, bevor klar ist, ob sie vom User gewollt sind.
2. Sobald Freigabe/Limit wieder verfügbar:
   - `dotnet test tests/PVAD.Migration.Pdf.Code.ActivePdf.Tests/PVAD.Migration.Pdf.Code.ActivePdf.Tests.csproj --no-restore --no-build -nodeReuse:false`
   - `dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --no-build -nodeReuse:false`
3. PDFTools gegen echte Pdftools-SDK-Samples validieren:
   - Besonders Direct-Generation-Klassen, Koordinatenursprung, Text-/Font-API und Save/Export-Semantik prüfen.
   - Danach Status ggf. von cautious pilot auf detaillierter Pilot anheben.
4. PDF Toolbox SDK / Toolbox add-on weiter härten:
   - API-Smoke-Test und UI-Beispiel final prüfen.
   - Output-Stream/FileStream-Setup nur dann automatisch entfernen/ersetzen, wenn `outPath` sicher erkannt wurde.
   - PageSize-Mapping und Save-Semantik für PVAD.Pdf sauber definieren.
5. Wenn Tests grün:
   - Checklisten final prüfen.
   - `git diff --check` auf Quelländerungen und idealerweise gesamtem Diff nach Artefakt-Cleanup.
5. Danach mögliche Architektur-Aufräumarbeiten:
   - Gemeinsame Helper für wiederkehrende Migration-Muster extrahieren.
   - Diagnostics-Konventionen vereinheitlichen.
   - Provider-neutralen Mapping-Report ausbauen.
   - Real-World-Samples je Anbieter sammeln und Snapshot-Tests ergänzen.

## Wichtige Kommandos

Provider-/API-Builds:

```bash
dotnet build tests/PVAD.Migration.Pdf.Code.ActivePdf.Tests/PVAD.Migration.Pdf.Code.ActivePdf.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1 -nodeReuse:false
dotnet build tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1 -nodeReuse:false
```

Tests, wenn Eskalation möglich:

```bash
dotnet test tests/PVAD.Migration.Pdf.Code.ActivePdf.Tests/PVAD.Migration.Pdf.Code.ActivePdf.Tests.csproj --no-restore --no-build -nodeReuse:false
dotnet test tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --no-restore --no-build -nodeReuse:false
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
- `tests/PVAD.Api.Tests/MigrationServiceTests.cs`
- `PVAD.WebApi/Services/Converters/*PdfConverter.cs`
- die drei letzten Provider unter `src/PVAD.Migration.*`

Dann den aktuellen Working Tree sauber einordnen und erst danach weiterarbeiten.
