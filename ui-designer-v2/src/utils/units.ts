export const UNIT_TO_PX: Record<string, number> = {
  px: 1,
  pt: 1,
  mm: 2.8346,
  cm: 28.346,
  in: 72,
};

export const toDisplay = (px: number, unit: string): number =>
  Math.round((px / (UNIT_TO_PX[unit] ?? 1)) * 100) / 100;

export const fromDisplay = (val: number, unit: string): number =>
  Math.round(val * (UNIT_TO_PX[unit] ?? 1));
