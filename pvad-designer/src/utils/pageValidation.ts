import type { PageSettings } from '../types';

export interface PageWarning {
  key: string;
  message: string;
}

export const getPageSettingsWarnings = (ps: PageSettings): PageWarning[] => {
  const warnings: PageWarning[] = [];
  const { headerEnabled, headerHeight, footerEnabled, footerHeight, margins, bleedSize, width, height, pagination } = ps;

  if (width < 100 || height < 100)
    warnings.push({ key: 'size-min', message: 'Page dimensions must be at least 100 px.' });
  if (width > 5000 || height > 5000)
    warnings.push({ key: 'size-max', message: 'Page dimensions exceed the 5 000 px maximum.' });
  if (headerEnabled && footerEnabled && headerHeight + footerHeight >= height * 0.8)
    warnings.push({ key: 'header-footer', message: 'Header + footer exceed 80% of page height.' });
  if (margins.top + margins.bottom >= height)
    warnings.push({ key: 'margins-v', message: 'Top and bottom margins exceed page height.' });
  if (margins.left + margins.right >= width)
    warnings.push({ key: 'margins-h', message: 'Left and right margins exceed page width.' });
  if (bleedSize > Math.min(width, height) / 4)
    warnings.push({ key: 'bleed', message: 'Bleed size is unusually large for this page.' });
  if (['odd-page', 'even-page'].includes(pagination.sectionStartBehavior))
    warnings.push({ key: 'pagination', message: 'Odd/even page section starts may produce blank pages.' });

  return warnings;
};
