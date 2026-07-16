# PXA.Documentation Website Checklist

## Ziel
`PXA.Documentation` ist die ausfuehrliche technische Dokumentationswebsite fuer PXA. Sie trennt Editor-/Produkt-Dokumentation von Code-/SDK-Dokumentation und wird zur Source of Truth fuer aktuelle Nutzung.

## Referenz
- Editor-/Produkt-Doku orientiert sich an Syncfusion Help: produktorientierte Navigation, Uebersicht, Konzepte, Beispiele.
- Code-/SDK-Doku orientiert sich an Syncfusion SDK-Seiten: Feature-Seiten, Codebeispiele, API-Links, Ressourcen.

## Zielstruktur
- [x] Website/App unter `websites/PXA.Documentation` planen oder bestehende `docs`-DocFX-Site dorthin ueberfuehren.
- [x] Gemeinsame Hauptnavigation definieren: Company, Products, Documentation, Demo, Pricing, About, Support.
- [x] Contact Sales als globale Header-CTA ergaenzen.
- [x] Live Demo als globale Header-CTA zu `PXA Designer` ergaenzen.
- [x] Documentation Home erstellen:
  - [x] PXA Overview
  - [x] Quickstarts
  - [x] Installation
  - [x] Concepts
- [x] Editor-Dokumentation strukturieren:
  - [x] Designer
  - [x] Templates
  - [x] Elements
  - [x] Element Reference mit editierbarem JSON, Run Button und Previewer unter dem JSON darstellen.
  - [x] Localization mit aktiven Sprachen, localizedProperties, RTL, elementLanguage und multi-language Export dokumentieren.
  - [x] Data Binding mit binding, expression, visibleExpression und repeat dokumentieren.
  - [x] PDF Viewer
  - [x] Spreadsheet
  - [x] Importer
  - [x] Export
- [x] Code-Dokumentation strukturieren:
  - [x] Code SDK nach Produktbereich strukturieren: zuerst PDF, danach Spreadsheet.
  - [x] PDF SDK in Unterbereiche strukturieren: PDF Generator, PDF Page Content, PDF Styling, PDF Analysis / Diagnostics.
  - [x] Spreadsheet SDK in Unterbereiche strukturieren: Workbook/Sheets, Cells/Styles/Columns, Formulas/Layout/Ranges, Import/Export/File IO, Operations/Validation/Calculation, Data/Design Conversion.
  - [x] Word / Export und Converter / Exporter als eigene Export-Dokumentationsbereiche fuehren.
  - [x] Word / Export und Converter / Exporter mit konkreten C# Beispielen dokumentieren.
  - [x] Localization und Data Binding auch als C# Template-SDK Beispiele dokumentieren.
  - [x] RTL und LTR Text Direction als eigenes C# Template-SDK Beispiel dokumentieren.
  - [x] Page Settings und Document Encryption/Protection als C# Template-SDK Beispiele dokumentieren.
  - [x] SDK-Beispiele statt Solution-Struktur: PDF Generator, Text, Image, Table, Chart, Barcode/QR, Form Field, Line/Shape, Watermark, Styling, Diagnostics, Spreadsheet Workbook/Formulas/Styles/Import/Operations/Data.
  - [x] Pro SDK-Beispiel interaktiven Playground mit Sprach-Tabs, editierbarem C# Code, Run Button, JSON, Model und Preview anzeigen.
  - [x] Java/Python-Sprachen als vorbereitete Tabs mit Coming-soon-Zustand anzeigen.
- [x] Migration Guides strukturieren:
  - [x] PDF Code Migration
  - [x] Report Designer Migration
  - [x] Spreadsheet Code Migration
  - [x] Provider Taxonomy
- [x] Cookbook-Seiten planen:
  - [x] PDF generation
  - [x] Edit PDF
  - [x] Forms
  - [x] Annotations
  - [x] Reports
  - [x] Import/export
- [x] API Reference via DocFX/OpenAPI integrieren.
- [x] Bestehende Checklists als Historie verlinken, nicht als Source of Truth verwenden.
- [x] PXA.Demo-Beispiele als Demo-Examples-Bereich verlinken.
- [x] Documentation-Strategie fuer Docs-Einbindung, Suche und Versionierung festlegen.

## MVP
- [x] Produktorientiertes TOC erstellen.
- [x] Startseite mit zwei Einstiegspfaden: Editor benutzen und Code integrieren.
- [x] Startseite auf vier technische Einstiegspfade erweitern: Editor, Code, Migration und API Reference.
- [x] Editor-, Code- und Migration-Bereiche mit Read-first, Common-tasks und Related-links Panels ausbauen.
- [x] Detaillierte Editor Docs fuer Designer, Templates, Elements, PDF Viewer, Spreadsheet, Importer und Export ausbauen.
- [x] Designer Workflow detaillieren:
  - [x] Template erstellen/oeffnen.
  - [x] Elemente hinzufuegen, positionieren und skalieren.
  - [x] Properties Panel, Preview, Export und Migration-Handoff dokumentieren.
  - [x] Designer-Bereiche Toolbar, Arbeitsflaeche, Properties, Preview, JSON/Export und Migration-Handoff erklaeren.
- [x] Editor Elements Reference als echte Anwenderdoku ausbauen:
  - [x] Gemeinsame Element-Attribute dokumentieren.
  - [x] Gemeinsame Style-Attribute dokumentieren.
  - [x] Text, Rich Text, Image, Table, Chart, Line/Shapes und Form Fields mit How-to-use, Use cases, Attributen und JSON-Beispielen dokumentieren.
  - [x] Alle 38 Designer-Elementtypen aus `pxa-designer/src/types.ts` als eigene Referenzkarten abdecken.
  - [x] Visuelle Designer-Erklaerungen/Screenshot-Panels fuer Toolbar, Arbeitsflaeche und Properties Panel ergaenzen.
- [x] Cookbook- und API-Reference-Bereiche mit Task-Karten, Status und Referenzhinweisen ausbauen.
- [x] Mindestens je eine Quickstart-Seite fuer Editor und Code.
- [x] Migration-Uebersicht mit Links zu Provider-Guides.
- [x] Migration-Detaildoku ausbauen:
  - [x] Common Migration Workflow dokumentieren.
  - [x] PDF Code Migration mit 15 Provider-Status-/Mapping-Zeilen dokumentieren.
  - [x] Report Designer Migration mit 8 Provider-/Format-Zeilen dokumentieren.
  - [x] Spreadsheet Code Migration mit 8 Provider-Zeilen dokumentieren.
  - [x] Provider Taxonomy und Migration Diagnostics als eigene Detailbereiche dokumentieren.
- [x] API Reference aus bestehender DocFX-Struktur erreichbar machen.
- [x] Demo-Beispiele mit Demo-Route, Input, Output und Source verlinken.
- [x] MVP-Strategie fuer bestehende `docs` Inhalte dokumentieren.

## Akzeptanzkriterien
- [x] "Editor benutzen" und "Code integrieren" sind klar getrennt.
- [x] TOC ist produktorientiert statt nur dateiorientiert.
- [x] Aktuelle PXA-Namen werden verwendet.
- [x] Historische Checklists behalten Kontext, ersetzen aber nicht die Docs.
- [x] Links zu `PXA.Company` und `PXA.Demo` sind vorhanden.
- [x] About, Products und Contact Sales sind im Header vorhanden.
- [x] Products, Pricing, About und Support bleiben direkte Menuepunkte in der Hauptnavigation.
- [x] Gemeinsamer Footer mit Product, Resources, Company, Developers und Legal ist vorhanden.
- [x] Footer enthaelt Copyright, Terms-Link und Privacy-Link.
- [x] Demo-Beispiele sind aus der Documentation erreichbar.
- [x] Documentation-Sidebar markiert den aktuell geklickten oder gescrollten Abschnitt.
- [x] Element-Reference-Fokusmodus: Klick auf ein Element zeigt nur diese Elementkarte, Overview zeigt wieder alle.
- [x] Sidebar-Fokusmodus auf alle linken Navigationseintraege erweitern: Designer, Editor-Details, Code SDK, Migration und Elemente zeigen nur den gewaehlten Content.

## Tests
- [x] `docfx build docs/docfx.json` oder entsprechender Build fuer `PXA.Documentation`.
- [x] Interne Links pruefen.
- [x] API Reference und OpenAPI-Seiten pruefen.
- [x] Mobile/Desktop Smoke-Test durchfuehren.
- [x] Suche nach veralteten `Canvas`-Aussagen in neuen Docs durchfuehren.
- [x] Documentation-Strategie-Datei pruefen.

## Offene Entscheidungen
- [x] Entscheiden, ob `docs` direkt umbenannt/verschoben oder in `websites/PXA.Documentation` eingebunden wird: bestehende `docs` bleiben im MVP an Ort und werden aus `PXA.Documentation` verlinkt.
- [x] Suchloesung festlegen: clientseitige Suche ueber den Documentation-Homepage-Index im MVP, spaeter Fulltext fuer `docs`.
- [x] Versionierungsmodell fuer Docs festlegen: MVP-Version ist `current`, spaeter `latest`/Release-Archiv.
