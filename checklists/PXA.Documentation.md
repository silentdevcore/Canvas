# PXA.Documentation Website Checklist

## Ziel
`PXA.Documentation` ist die ausfuehrliche technische Dokumentationswebsite fuer PXA. Sie trennt Editor-/Produkt-Dokumentation von Code-/SDK-Dokumentation und wird zur Source of Truth fuer aktuelle Nutzung.

## Referenz
- Editor-/Produkt-Doku orientiert sich an Syncfusion Help: produktorientierte Navigation, Uebersicht, Konzepte, Beispiele.
- Code-/SDK-Doku orientiert sich an Syncfusion SDK-Seiten: Feature-Seiten, Codebeispiele, API-Links, Ressourcen.

## Zielstruktur
- [x] Website/App unter `websites/PXA.Documentation` planen oder bestehende `docs`-DocFX-Site dorthin ueberfuehren.
- [x] Gemeinsame Hauptnavigation definieren: Company, Documentation, Demo, Pricing, Support.
- [x] Documentation Home erstellen:
  - [x] PXA Overview
  - [x] Quickstarts
  - [x] Installation
  - [x] Concepts
- [x] Editor-Dokumentation strukturieren:
  - [x] Designer
  - [x] Templates
  - [x] Elements
  - [x] PDF Viewer
  - [x] Spreadsheet
  - [x] Importer
  - [x] Export
- [x] Code-Dokumentation strukturieren:
  - [x] PXA.Generator
  - [x] PXA.Migration
  - [x] PXA.Importer
  - [x] PXA.Infrastructure
  - [x] PXA.WebApi
  - [x] API Reference
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
- [ ] Bestehende Checklists als Historie verlinken, nicht als Source of Truth verwenden.

## MVP
- [x] Produktorientiertes TOC erstellen.
- [x] Startseite mit zwei Einstiegspfaden: Editor benutzen und Code integrieren.
- [ ] Mindestens je eine Quickstart-Seite fuer Editor und Code.
- [x] Migration-Uebersicht mit Links zu Provider-Guides.
- [x] API Reference aus bestehender DocFX-Struktur erreichbar machen.

## Akzeptanzkriterien
- [x] "Editor benutzen" und "Code integrieren" sind klar getrennt.
- [x] TOC ist produktorientiert statt nur dateiorientiert.
- [x] Aktuelle PXA-Namen werden verwendet.
- [ ] Historische Checklists behalten Kontext, ersetzen aber nicht die Docs.
- [x] Links zu `PXA.Company` und `PXA.Demo` sind vorhanden.

## Tests
- [x] `docfx build docs/docfx.json` oder entsprechender Build fuer `PXA.Documentation`.
- [x] Interne Links pruefen.
- [x] API Reference und OpenAPI-Seiten pruefen.
- [x] Mobile/Desktop Smoke-Test durchfuehren.
- [x] Suche nach veralteten `Canvas`-Aussagen in neuen Docs durchfuehren.

## Offene Entscheidungen
- [ ] Entscheiden, ob `docs` direkt umbenannt/verschoben oder in `websites/PXA.Documentation` eingebunden wird.
- [ ] Suchloesung festlegen.
- [ ] Versionierungsmodell fuer Docs festlegen.
