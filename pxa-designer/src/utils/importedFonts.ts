import type { SimpleElement } from '@/types';

type ImportedFontFace = {
  family: string;
  dataUri: string;
  format: string;
  weight?: string;
  style?: string;
};

export function buildImportedFontFaceCss(elements: SimpleElement[]): string {
  const faces = new Map<string, ImportedFontFace>();

  for (const element of elements) {
    const style = element.style ?? {};
    const family = typeof style.fontFamily === 'string' ? style.fontFamily : '';
    const dataUri = typeof style.fontDataUri === 'string' ? style.fontDataUri : '';
    const format = typeof style.fontFormat === 'string' ? style.fontFormat : '';

    if (!family || !dataUri || !format) continue;

    const weight = typeof style.fontWeight === 'string' ? style.fontWeight : 'normal';
    const fontStyle = typeof style.fontStyle === 'string' ? style.fontStyle : 'normal';
    faces.set(`${family}|${weight}|${fontStyle}|${format}`, {
      family,
      dataUri,
      format,
      weight,
      style: fontStyle,
    });
  }

  return [...faces.values()].map(face => `
@font-face {
  font-family: '${escapeCssString(face.family)}';
  src: url('${face.dataUri}') format('${escapeCssString(face.format)}');
  font-weight: ${face.weight ?? 'normal'};
  font-style: ${face.style ?? 'normal'};
  font-display: swap;
}`).join('\n');
}

export function installImportedFontFaces(styleElementId: string, elements: SimpleElement[]): void {
  if (typeof document === 'undefined') return;

  const css = buildImportedFontFaceCss(elements);
  let styleElement = document.getElementById(styleElementId) as HTMLStyleElement | null;

  if (!css) {
    styleElement?.remove();
    return;
  }

  if (!styleElement) {
    styleElement = document.createElement('style');
    styleElement.id = styleElementId;
    document.head.appendChild(styleElement);
  }

  styleElement.textContent = css;
}

function escapeCssString(value: string): string {
  return value.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
}
