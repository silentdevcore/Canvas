# PXA Website Hosting Checklist

## Ziel
Hosting fuer `PXA.Company`, `PXA.Documentation` und `PXA.Demo` so vorbereiten, dass die drei Websites getrennt gebaut, geprueft und spaeter getrennt deployed werden koennen.

## Deployment-Ziele
- [x] `PXA.Company` als Marketing-/Sales-Site planen.
- [x] `PXA.Documentation` als technische Docs-Site planen.
- [x] `PXA.Demo` als Demo-Galerie planen.
- [x] Public Host-Empfehlungen festlegen:
  - [x] `powerdoxautomation.com`
  - [x] `docs.powerdoxautomation.com`
  - [x] `demos.powerdoxautomation.com`

## Build-Artefakte
- [x] `websites/PXA.Company/dist`
- [x] `websites/PXA.Documentation/dist`
- [x] `websites/PXA.Demo/dist`
- [x] `dist` bleibt Git-ignoriert.
- [x] `websites/shared` bleibt Source-only und wird je Site gebundled.

## Lokale Ports
- [x] Company Dev: `http://localhost:5173/`
- [x] Documentation Dev: `http://localhost:5174/`
- [x] Demo Dev: `http://localhost:5175/`
- [x] Company Preview: `http://localhost:4173/`
- [x] Documentation Preview: `http://localhost:4174/`
- [x] Demo Preview: `http://localhost:4175/`

## Pre-Deploy Checks
- [x] Alle drei Website-Builds laufen lassen.
- [x] `git diff --check` ausfuehren.
- [x] Legacy-Branding-Suche fuer `Canvas` in Website-Dateien ausfuehren.
- [x] Lokale URLs smoke-testen.
- [x] PXA.Demo Example-Dateien als synthetische Public Assets dokumentieren.
- [x] Environment-aware Site URLs zentralisieren.
- [x] Company Clean Paths fuer Products, Pricing, About, Support und Contact vorbereiten.
- [x] Company Clean Paths setzen Title und Meta Description im SPA-MVP.

## Offene Post-MVP Aufgaben
- [x] Environment-aware Site URLs einfuehren.
- [ ] CI/build-time Env-Var-Injection fuer Production URLs einrichten.
- [ ] Production Rewrite/Fallback fuer Company Clean Paths einrichten.
- [ ] CI-Build fuer alle drei Websites einrichten.
- [ ] Link-Check fuer Cross-Site-Links und Demo-Example-Dateien einfuehren.
- [ ] Konkretes Hosting-Ziel auswaehlen und Deployment-Konfiguration anlegen.
- [ ] Production Deployment pro Website durchfuehren.
