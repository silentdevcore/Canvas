# PXA Web Design System Checklist

## Ziel
Ein gemeinsames Layout- und Design-System fuer `PXA.Company`, `PXA.Documentation` und `PXA.Demo`. Alle drei Websites sollen wie ein Produkt wirken, aber unterschiedliche Nutzungsmodi unterstuetzen: Marketing, technische Dokumentation und interaktive Demos.

## Grundsaetze
- [ ] Gemeinsames Branding: Power Dox Automation / PXA.
- [ ] Gemeinsame Navigation: Company, Documentation, Demo, Pricing, Support.
- [ ] Ruhige, professionelle B2B-Optik.
- [ ] Funktionale, scanbare Layouts fuer Docs und Demo.
- [ ] Marketing darf emotionaler sein, bleibt aber technisch glaubwuerdig.
- [ ] Keine alten `Canvas`-Brandingreste.

## Shared Struktur
- [x] `websites/shared` als gemeinsame Grundlage anlegen.
- [x] `websites/README.md` mit lokalen Ports und Build-Kommandos anlegen.
- [x] Gemeinsame CSS-Tokens definieren.
- [x] Gemeinsame Basisstyles definieren.
- [x] Gemeinsame Layoutstyles definieren.
- [x] Gemeinsame Komponentenstyles definieren.
- [x] README mit Nutzungsregeln ergaenzen.

## Design Tokens
- [x] Farben fuer Brand, Neutral, Success, Warning, Danger definieren.
- [x] Typografie fuer UI, Fliesstext und Code definieren.
- [x] Spacing-Skala definieren.
- [x] Radius-Skala definieren.
- [x] Shadows definieren.
- [x] Breakpoints definieren.
- [x] Focus-, Hover-, Active- und Disabled-States definieren.

## Layout Templates
- [ ] `AppShell` fuer Header, Main und Footer planen.
- [x] `MarketingLayout` fuer `PXA.Company` planen.
- [x] `DocsLayout` fuer `PXA.Documentation` planen.
- [x] `DemoLayout` fuer `PXA.Demo` planen.
- [ ] Responsive Navigation planen.
- [ ] Footer mit Crosslinks zwischen den drei Websites planen.

## Shared Components
- [ ] Button
- [ ] LinkButton
- [ ] Card
- [ ] ProductCard
- [ ] FeatureGrid
- [ ] DemoCard
- [ ] StatusBadge
- [ ] CodeBlock
- [ ] SearchInput
- [ ] PageHeader
- [ ] Section
- [ ] SiteFooter

## MVP
- [x] `websites/shared/styles/tokens.css`
- [x] `websites/shared/styles/base.css`
- [x] `websites/shared/styles/layouts.css`
- [x] `websites/shared/styles/components.css`
- [x] `websites/shared/styles/index.css`
- [x] `websites/shared/README.md`
- [x] Drei Layout-Modi dokumentieren: company, documentation, demo.
- [x] Statische Templates fuer Company, Documentation und Demo anlegen.

## Akzeptanzkriterien
- [ ] Alle drei Websites koennen dieselben Tokens und Basisstyles verwenden.
- [ ] Header/Footer-Pattern ist konsistent.
- [ ] Buttons, Cards, Badges und Codebloecke haben einheitliche States.
- [ ] Mobile Layouts sind vorbereitet.
- [ ] Die Palette ist nicht einfarbig und nicht zu verspielt.
- [ ] Texte passen in Buttons, Cards und Navigation.

## Tests
- [ ] CSS-Dateien auf Syntax und Import-Reihenfolge pruefen.
- [x] Suche nach `Canvas` in neuen Dateien.
- [x] Build in erster Website-App durchfuehren.
- [x] Smoke-Test in erster Website-App durchfuehren.
- [x] Alle drei Website-Apps parallel lokal starten.
- [x] HTTP 200 fuer `PXA.Company`, `PXA.Documentation` und `PXA.Demo` pruefen.
- [x] Desktop-Screenshots fuer alle drei Websites pruefen.
- [x] Mobile-Overflow-Regeln fuer Header, Container und responsive Layouts fixen.
- [ ] Mobile/Desktop Screenshot-Pruefung durchfuehren, sobald die Websites laufen.

## Offene Entscheidungen
- [ ] Logo-Datei oder reines Textlogo festlegen.
- [ ] Finales Pricing-/Sales-Ziel festlegen.
- [ ] Ob `websites/shared` spaeter ein eigenes Package wird, festlegen.
