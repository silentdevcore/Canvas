import type { Page, PageLayoutSettings, PageSettings } from '@/types';

const validDimension = (value: number | undefined, fallback: number) =>
  Number.isFinite(value) && value! > 0 && value! <= 14400 ? value! : fallback;

export function resolvePageLayout(defaults: PageSettings, layout?: PageLayoutSettings): PageLayoutSettings {
  let width = validDimension(layout?.width, defaults.width);
  let height = validDimension(layout?.height, defaults.height);
  const orientation = layout?.orientation ?? defaults.orientation ?? (width > height ? 'landscape' : 'portrait');
  if ((orientation === 'landscape' && width < height) || (orientation === 'portrait' && width > height)) {
    [width, height] = [height, width];
  }
  const sourceMargins = { ...defaults.margins, ...(layout?.margins ?? {}) };
  return {
    width,
    height,
    orientation,
    backgroundColor: layout?.backgroundColor ?? defaults.backgroundColor ?? '#ffffff',
    backgroundImage: layout?.backgroundImage ?? defaults.backgroundImage ?? '',
    backgroundImageFit: layout?.backgroundImageFit ?? defaults.backgroundImageFit ?? 'cover',
    margins: {
      top: Math.max(0, sourceMargins.top),
      right: Math.max(0, sourceMargins.right),
      bottom: Math.max(0, sourceMargins.bottom),
      left: Math.max(0, sourceMargins.left),
    },
    bleedSize: Math.min(
      Math.max(0, layout?.bleedSize ?? defaults.bleedSize ?? 0),
      Math.min(width, height) / 2 - 0.01,
    ),
  };
}

export function effectivePageSettings(defaults: PageSettings, page?: Page): PageSettings {
  return { ...defaults, ...resolvePageLayout(defaults, page?.layout) } as PageSettings;
}

export function materializePageLayout(settings: PageSettings): PageLayoutSettings {
  return {
    width: settings.width,
    height: settings.height,
    orientation: settings.orientation,
    backgroundColor: settings.backgroundColor,
    backgroundImage: settings.backgroundImage,
    backgroundImageFit: settings.backgroundImageFit,
    margins: { ...settings.margins },
    bleedSize: settings.bleedSize,
  };
}
