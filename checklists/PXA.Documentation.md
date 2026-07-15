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
- [x] Bestehende Checklists als Historie verlinken, nicht als Source of Truth verwenden.
- [x] PXA.Demo-Beispiele als Demo-Examples-Bereich verlinken.
- [x] Documentation-Strategie fuer Docs-Einbindung, Suche und Versionierung festlegen.

## MVP
- [x] Produktorientiertes TOC erstellen.
- [x] Startseite mit zwei Einstiegspfaden: Editor benutzen und Code integrieren.
- [x] Startseite auf vier technische Einstiegspfade erweitern: Editor, Code, Migration und API Reference.
- [x] Editor-, Code- und Migration-Bereiche mit Read-first, Common-tasks und Related-links Panels ausbauen.
- [x] Mindestens je eine Quickstart-Seite fuer Editor und Code.
- [x] Migration-Uebersicht mit Links zu Provider-Guides.
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
- [x] Footer enthaelt Copyright und AGB-Link.
- [x] Demo-Beispiele sind aus der Documentation erreichbar.

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
