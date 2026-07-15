# PXA.Company Website Checklist

## Ziel
`PXA.Company` ist die Marketing- und Verkaufswebsite fuer Power Dox Automation. Sie soll erklaeren, Vertrauen aufbauen und Besucher zu Demo, Documentation oder Kontakt fuehren.

## Referenz
- Aufbau orientiert sich an GemBox: Produkt-Hub, klare Produktkarten, Support, Pricing, News und Trust-Signale.
- Keine 1:1-Kopie; PXA bleibt technisch, ruhig und B2B-orientiert.

## Zielstruktur
- [x] Website/App unter `websites/PXA.Company` planen.
- [x] Gemeinsame Hauptnavigation definieren: Company, Products, Documentation, Demo, Pricing, About, Support.
- [x] Contact Sales als globale Header-CTA ergaenzen.
- [x] Hero fuer Power Dox Automation erstellen.
- [x] About-Sektion fuer Produkt-/Firmenkontext erstellen.
- [x] CTAs definieren: Demo ansehen, Documentation lesen, Sales kontaktieren.
- [x] Produktkarten erstellen:
  - [x] PXA Generator
  - [x] PXA Migration
  - [x] PXA Importer
  - [x] PXA Designer
  - [x] PXA PDF Viewer
  - [x] PXA Spreadsheet
- [x] Use-Case-Sektionen erstellen:
  - [x] PDF-Erstellung
  - [x] Designer-Migration
  - [x] Code-Migration
  - [x] Import/Export
  - [x] Reports
  - [x] Spreadsheet
- [x] Trust/Proof-Sektion erstellen:
  - [x] Unterstuetzte Provider
  - [x] Migration-Abdeckung
  - [x] Beispielberichte
  - [x] Feature-Parity Roadmap
- [x] Pricing/Trial/Contact als vorbereitete Sektionen anlegen.
- [x] Support/News/Roadmap-Sektionen anlegen.

## MVP
- [x] Home-Seite mit Hero, Produktuebersicht und CTAs.
- [x] Produktuebersicht mit mindestens sechs Produktkarten.
- [x] About-Seite/-Sektion mit Produktmission und Prinzipien.
- [x] Statische Company-Seiten fuer `products.html`, `pricing.html`, `about.html`, `support.html` und `contact.html` vorbereiten.
- [x] Statische Legal-Seiten fuer `terms.html`, `privacy.html` und `license.html` vorbereiten.
- [x] Statische Produktdetailseiten unter `products/` fuer Generator, Migration, Importer, Designer, PDF Viewer und Spreadsheet vorbereiten.
- [x] Produktdetailseiten mit Hero, Best-Fit, Workflows, Capabilities und Integrationspunkten ausbauen.
- [x] Route-Metadaten fuer statische Company-Seiten setzen.
- [x] Jede statische Company-Seite rendert nur ihren eigenen Hauptinhalt mit gemeinsamer Navigation und gemeinsamem Footer.
- [x] Produktkarten fuehren zu eigener Produktseite, Demo und Documentation.
- [x] Company Home und Products Overview mit erstem produktnahen B2B-Content ersetzen.
- [x] Pricing, About und Support mit produktnahen B2B-Inhalten ausbauen.
- [x] Contact, Terms, Privacy und License mit strukturierten Draft-Inhalten ausbauen.
- [x] Use-Case-Uebersicht mit Links zu `PXA.Documentation`.
- [x] Demo-CTA mit Link zu `PXA.Demo`.
- [x] Produktkarten mit direkten Demo- und Documentation-Links.
- [x] Showcase-Karten mit direkten Demo- und Documentation-Links.
- [x] Kontaktseite mit Sales-, Migration-Assessment- und Technical-Evaluation-Pfaden.
- [x] Company-Strategie fuer Domain, Pricing/Trial und Kontaktweg dokumentieren.

## Akzeptanzkriterien
- [x] Klare Produktnavigation ist vorhanden.
- [x] Besucher koennen von Company zu Documentation und Demo wechseln.
- [x] Products-Menuepunkt ist in allen Website-Headern vorhanden.
- [x] About-Menuepunkt ist in allen Website-Headern vorhanden.
- [x] Products, Pricing, About und Support bleiben direkte Menuepunkte in der Hauptnavigation.
- [x] Contact Sales ist in allen Website-Headern vorhanden.
- [x] Gemeinsamer Footer mit Product, Resources, Company, Developers und Legal ist vorhanden.
- [x] Footer enthaelt Copyright, Terms-Link und Privacy-Link.
- [x] Produkt- und Beispielbereiche fuehren direkt zu passenden Demo-/Docs-Einstiegen.
- [x] Keine alten `Canvas`-Brandingreste in neuen Website-Inhalten.
- [x] Desktop und Mobile sind lesbar und nicht ueberladen.
- [x] Pricing/Trial-Texte sind als Platzhalter erkennbar, bis echte Lizenzentscheidungen stehen.
- [x] Domain-, Pricing- und Kontaktentscheidungen sind fuer den MVP festgehalten.

## Tests
- [x] Build fuer `PXA.Company` ausfuehren, sobald die App existiert.
- [x] Interne Links pruefen.
- [x] Mobile/Desktop Smoke-Test durchfuehren.
- [x] Suche nach `Canvas` in neuen Website-Dateien durchfuehren.
- [x] Company-Strategie-Datei pruefen.

## Offene Entscheidungen
- [x] Domain/Subdomain festlegen: empfohlen `powerdoxautomation.com`, `docs.powerdoxautomation.com`, `demos.powerdoxautomation.com`.
- [x] Pricing- und Trial-Modell festlegen: MVP bleibt Placeholder mit Trial, Team und Enterprise Pfaden.
- [x] Kontaktweg festlegen: MVP bleibt Sales/Contact-Placeholder; erste Umsetzung spaeter Mail/Static-Form, CRM/Ticketing post-MVP.
