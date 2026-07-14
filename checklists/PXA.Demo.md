# PXA.Demo Website Checklist

## Ziel
`PXA.Demo` ist die interaktive Demo-Website fuer PXA. Sie soll Beispiele katalogisieren, live erlebbar machen und jeweils zu Source, Documentation und passenden Checklists fuehren.

## Referenz
- Aufbau orientiert sich an DevExpress Demos: Demo-Katalog, Kategorien, kurze Erklaerung und direkte "Open demo"-Einstiege.
- Unfertige Demos zeigen Status statt leere Seiten.

## Zielstruktur
- [x] Website/App unter `websites/PXA.Demo` planen.
- [x] Gemeinsame Hauptnavigation definieren: Company, Documentation, Demo, Pricing, Support.
- [x] Demo Home erstellen:
  - [x] Kategorien
  - [x] Suche
  - [x] Filter
  - [x] Statusanzeige
- [x] Demo-Karten definieren:
  - [x] Titel
  - [x] Kurzbeschreibung
  - [x] Tags
  - [x] Status
  - [x] Open demo
  - [x] View source
  - [x] Docs
- [x] Demo-Detailseite definieren:
  - [x] Live-Preview
  - [x] Eingabedaten
  - [x] Ergebnis
  - [x] JSON/Code
  - [x] Download
  - [x] Links zu Docs und Checklists
  - [x] Direkte Detail-Routen pro Demo via `#demo/<id>`
  - [x] Demo-spezifische Preview-Flaechen fuer Receipt, Report, Chart, Table, Viewer, Migration und Importer
  - [x] Segmentierte Detailansicht fuer Preview, Input, Output und Code
- [x] Kategorien anlegen:
  - [x] PDF Generator
  - [x] PDF Viewer
  - [x] Designer
  - [x] Report Migration
  - [x] Code Migration
  - [x] Spreadsheet
  - [x] Import/Export

## Erste Demos
- [x] Invoice / Booking Receipt
- [x] Master-detail report
- [x] Chart report
- [x] Rich text / table report
- [x] PDF viewer annotations/forms
- [x] Spreadsheet import/export
- [x] Provider migration examples

## MVP
- [x] MVP-Basis abgeschlossen: Galerie, Detailansichten, Live Booking Receipt und Beispiel-Dateien fuer alle Demo-Karten.
- [x] Demo-Galerie mit Kategorien und Suche.
- [x] Zentrale statische Demo-Datenquelle in `src/demoData.js`.
- [x] Rendering, UI-Interaktionen und Demo-State in eigene Module getrennt.
- [x] Direkte Demo-Links fuer einzelne Detailansichten.
- [x] Erste konkrete runnable Preview fuer Invoice / Booking Receipt.
- [x] Live-Eingabemaske fuer Invoice / Booking Receipt mit aktualisierter Preview.
- [x] Sichtbare Source-, Checklist- und Download-Referenzen pro Demo.
- [x] Erste statisch gehostete Beispiel-Dateien fuer Booking Receipt unter `public/examples/booking-receipt`.
- [x] Statisch gehostete Beispiel-Dateien fuer Chart Report und Spreadsheet Import/Export.
- [x] Statisch gehostete Beispiel-Dateien fuer PDF Viewer und Provider Migration.
- [x] Statisch gehostete Beispiel-Dateien fuer Master-detail, Rich text/Table und File importer.
- [x] Alle Demo-Karten haben Source-, Input- und Output-Beispieldateien.
- [x] Demo-Karten verlinken direkt auf Source-Dateien, wenn sie vorhanden sind.
- [x] Mindestens fuenf Demo-Karten.
- [x] Mindestens eine lauffaehige Demo fuer PDF/Designer.
- [x] Mindestens eine lauffaehige Demo fuer Migration.
- [x] Mindestens eine lauffaehige Demo fuer Spreadsheet oder Import/Export.
- [x] Statusmodell fuer geplante, teilweise fertige und fertige Demos.

## Akzeptanzkriterien
- [x] Jede fertige Demo ist direkt startbar.
- [x] Unfertige Demos zeigen klaren Status und naechste Schritte.
- [x] Jede Demo verlinkt zu passender Documentation.
- [x] Jede Demo verlinkt zu Source oder Beispiel-Dateien, sobald vorhanden.
- [x] Hosting- und Sicherheitsgrenzen sind dokumentiert.
- [x] Keine alten `Canvas`-Brandingreste in neuen Demo-Inhalten.

## Tests
- [x] Build fuer `PXA.Demo` ausfuehren, sobald die App existiert.
- [x] Demo-Routen smoke-testen.
- [x] Filter/Suche testen.
- [x] Links zu Docs, Source und Checklists pruefen.
- [x] Beispiel-Dateien fuer Booking Receipt per Vite Public Path verlinken.
- [x] Beispiel-Dateien fuer Chart und Spreadsheet per Vite Public Path verlinken.
- [x] Beispiel-Dateien fuer PDF Viewer und Provider Migration per Vite Public Path verlinken.
- [x] Beispiel-Dateien fuer Master-detail, Rich text/Table und File importer per Vite Public Path verlinken.
- [x] Security-/Hosting-Notiz fuer Demo-MVP pruefen.
- [x] Mobile/Desktop Smoke-Test durchfuehren.

## Offene Entscheidungen
- [x] Demo-Datenquelle festlegen: statische Daten im MVP, spaeter API oder gemischter Ansatz.
- [x] Hosting fuer Beispiel-Dateien festlegen: statische Vite Public Assets unter `websites/PXA.Demo/public/examples/<demo-id>/`.
- [x] Sicherheitsgrenzen fuer Upload-/Live-Migration-Demos festlegen: keine Uploads/Live-Migration im statischen MVP; Details siehe `websites/PXA.Demo/SECURITY_AND_HOSTING.md`.
