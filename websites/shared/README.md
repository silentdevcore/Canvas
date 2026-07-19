# PXA Shared Web Design System

This folder contains shared layout and styling foundations for the PXA web properties:

- `PXA.Company`
- `PXA.Documentation`
- `PXA.Demo`
- `PXA.Account`

The goal is to keep the sites visually consistent while allowing each site to optimize for its own workflow: marketing, documentation, demos, or customer self-service.

## Style Entry

Use `styles/index.css` as the single import point for new website apps:

```css
@import "../shared/styles/index.css";
```

If a site lives deeper than one level below `websites`, adjust the relative import path.

## Layout Modes

Use these root classes on the page shell:

- `pxa-site pxa-site--company`
- `pxa-site pxa-site--documentation`
- `pxa-site pxa-site--demo`
- Account uses the same tokens and components inside its authentication and portal shells.

Each site should use the same header/footer pattern and the same token names. Site-specific styling should be layered after the shared CSS.

## Shared Navigation

Primary navigation should stay consistent:

- Company
- Documentation
- Demo
- Pricing
- Support

Site apps can map these labels to local routes, subdomains, or deployment URLs.

## Component Classes

The shared CSS provides stable classes for common components:

- `pxa-button`
- `pxa-card`
- `pxa-product-card`
- `pxa-demo-card`
- `pxa-status`
- `pxa-code`
- `pxa-search`
- `pxa-page-header`
- `pxa-section`
- `pxa-site-header`
- `pxa-site-footer`

Prefer extending these classes over redefining fundamentals in each site.

## Static Templates

Initial static templates live in `templates/`:

- `company.html`
- `documentation.html`
- `demo.html`

They are intentionally simple and can be opened directly in a browser. Use them as layout references when creating the actual website apps.
