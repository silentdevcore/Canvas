import contract from '../../../../product-metadata/designer-date-formatting.json';
import type { SimpleElement } from '@/types';

export type DateWarning = 'dateLocaleFallback' | 'dateTimezoneFallback' | 'dateHistoricalTimezoneFallback' | 'dateFormatFallback' | 'dateBindingUnresolved';
type Part = { token: string } | { literal: string };
export const DESIGNER_DATE_DEFAULTS = { format: contract.defaultFormat, locale: contract.defaultLocale, timezone: contract.defaultTimeZone };

function parsePattern(pattern: string): Part[] | null {
  if (!pattern || pattern.length > contract.maximumFormatLength) return null;
  const parts: Part[] = [];
  const tokens = contract.tokens as Record<string, number>;
  for (let i = 0; i < pattern.length; i++) {
    const c = pattern[i];
    if (c === "'" || c === '"') {
      let literal = '';
      const quote = c;
      while (++i < pattern.length && pattern[i] !== quote) {
        if (pattern[i] === '\\' && ++i >= pattern.length) return null;
        literal += pattern[i];
      }
      if (i >= pattern.length) return null;
      parts.push({ literal });
    } else if (c === '\\') {
      if (++i >= pattern.length) return null;
      parts.push({ literal: pattern[i] });
    } else if (c === '%') {
      if (++i >= pattern.length || !(tokens[pattern[i]] || '/:'.includes(pattern[i]))) return null;
      parts.push({ token: pattern[i] });
    } else if (tokens[c]) {
      const start = i;
      while (pattern[i + 1] === c) i++;
      if (i - start + 1 > tokens[c]) return null;
      parts.push({ token: pattern.slice(start, i + 1) });
    } else if (/[a-z]/i.test(c) && !contract.literalLetters.includes(c)) return null;
    else parts.push('/:'.includes(c) ? { token: c } : { literal: c });
  }
  return parts;
}

function parseBoundDate(source?: string): Date | null {
  if (!source || !/^\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2}))?$/.test(source)) return null;
  const date = new Date(source);
  const calendar = new Date(`${source.slice(0, 10)}T00:00:00Z`);
  if (source.startsWith('0000') || !Number.isFinite(date.getTime()) || !Number.isFinite(calendar.getTime())
    || calendar.toISOString().slice(0, 10) !== source.slice(0, 10)) return null;
  if (source.length > 10 && (Number(source.slice(11, 13)) > 23 || Number(source.slice(14, 16)) > 59 || Number(source.slice(17, 19)) > 59)) return null;
  const offset = /([+-])(\d{2}):(\d{2})$/.exec(source);
  if (offset && (Number(offset[2]) > 14 || Number(offset[3]) > 59 || Number(offset[2]) === 14 && Number(offset[3]) !== 0)) return null;
  return date;
}

export function formatDocumentDate(element: SimpleElement, now = new Date(), resolve: (value: string) => string | undefined = value => value): {
  text: string; warnings: DateWarning[];
} {
  if (element.dateMode !== 'render' && element.dateMode !== 'binding') return { text: element.content ?? element.fallbackText ?? '', warnings: [] };
  const warnings: DateWarning[] = [];
  let instant = now;
  let dateOnly = false;
  if (element.dateMode === 'binding') {
    const token = `{{${element.binding?.trim() ?? ''}}}`;
    const bound = element.binding?.trim() ? resolve(token) : undefined;
    const source = bound !== undefined && bound !== token ? bound : element.content;
    const value = parseBoundDate(source);
    if (!value) return { text: element.fallbackText ?? (element.binding?.trim() ? `{{ ${element.binding.trim()} }}` : ''), warnings: ['dateBindingUnresolved'] };
    instant = value;
    dateOnly = source?.length === 10;
  }

  let language = (element.locale || contract.defaultLocale).split('-')[0].toLowerCase();
  if (!/^[a-z]{2,3}(?:-[a-z0-9]{2,8})*$/i.test(element.locale || contract.defaultLocale) || !(language in contract.locales)) {
    language = 'en'; warnings.push('dateLocaleFallback');
  }
  const settings = contract.locales[language as keyof typeof contract.locales];
  let locale = settings.culture;
  let timezone = element.timezone?.trim() || contract.defaultTimeZone;
  timezone = (contract.timeZoneAliases as Record<string, string>)[timezone] ?? timezone;
  try {
    if (timezone !== 'UTC' && !timezone.includes('/')) throw new RangeError();
    new Intl.DateTimeFormat('en', { timeZone: timezone });
  } catch { timezone = 'UTC'; warnings.push('dateTimezoneFallback'); }
  let pattern = element.dateFormat?.trim() ? element.dateFormat : contract.defaultFormat;
  let utc = ['u', 'U', 'r', 'R'].includes(pattern);
  if (pattern === 'r' || pattern === 'R') locale = 'en-US';
  if (pattern.length === 1) {
    const shortDate = language === 'en' && element.locale?.toLowerCase() === 'en-gb' ? 'dd/MM/yyyy' : settings.shortDate;
    const standard: Record<string, string> = {
      d: shortDate, D: settings.longDate, t: settings.shortTime, T: settings.longTime,
      g: `${shortDate} ${settings.shortTime}`, G: `${shortDate} ${settings.longTime}`,
      f: `${settings.longDate} ${settings.shortTime}`, F: `${settings.longDate} ${settings.longTime}`, U: `${settings.longDate} ${settings.longTime}`,
      m: settings.monthDay, M: settings.monthDay, y: settings.yearMonth, Y: settings.yearMonth,
      ...contract.standardFormats,
    };
    pattern = standard[pattern] ?? '';
  }
  let parts = parsePattern(pattern);
  if (!parts) { parts = parsePattern(contract.defaultFormat)!; utc = false; warnings.push('dateFormatFallback'); }
  if (utc || dateOnly) timezone = 'UTC';
  if (timezone !== 'UTC' && instant.getTime() < Date.parse(contract.minimumZonedInstant)) {
    warnings.push('dateHistoricalTimezoneFallback');
    timezone = 'UTC';
  }

  const fields = new Intl.DateTimeFormat('en-US', { calendar: 'gregory', numberingSystem: 'latn', timeZone: timezone,
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
  }).formatToParts(instant);
  const field = (name: Intl.DateTimeFormatPartTypes) => fields.find(part => part.type === name)!.value;
  const year = Number(field('year')), month = Number(field('month')), day = Number(field('day')), hour = Number(field('hour'));
  const local = new Date(0);
  local.setUTCFullYear(year, month - 1, day);
  local.setUTCHours(hour, Number(field('minute')), Number(field('second')), 0);
  const offset = Math.round((local.getTime() - (instant.getTime() - instant.getUTCMilliseconds())) / 60000);
  const sign = offset < 0 ? '-' : '+', offsetHours = Math.floor(Math.abs(offset) / 60), offsetMinutes = Math.abs(offset) % 60;
  const pad = (value: number, length = 2) => String(value).padStart(length, '0');
  const named = (options: Intl.DateTimeFormatOptions, type: Intl.DateTimeFormatPartTypes) => new Intl.DateTimeFormat(locale, {
    calendar: 'gregory', numberingSystem: 'latn', timeZone: timezone, ...options,
  }).formatToParts(instant).find(part => part.type === type)?.value ?? '';
  const hasDay = parts.some(part => 'token' in part && ['d', 'dd'].includes(part.token));
  let text = '';
  for (const part of parts) {
    if ('literal' in part) { text += part.literal; continue; }
    const token = part.token, c = token[0], n = token.length;
    const value = c === 'y' ? (n <= 2 ? pad(year % 100, n) : pad(year, n))
      : c === 'M' ? (n <= 2 ? pad(month, n) : named({ month: n === 3 ? 'short' : 'long', ...(hasDay ? { day: 'numeric' } : {}) }, 'month'))
      : c === 'd' ? (n <= 2 ? pad(day, n) : named({ weekday: n === 3 ? 'short' : 'long' }, 'weekday'))
      : c === 'H' ? pad(hour, n) : c === 'h' ? pad(hour % 12 || 12, n)
      : c === 'm' ? pad(Number(field('minute')), n) : c === 's' ? pad(Number(field('second')), n)
      : c === 'f' || c === 'F' ? pad(instant.getUTCMilliseconds(), 3).slice(0, n).replace(c === 'F' ? /0+$/ : /$^/, '')
      : c === 't' ? (hour < 12 ? settings.am : settings.pm).slice(0, n === 1 ? 1 : undefined)
      : c === 'z' || c === 'K' ? `${sign}${pad(offsetHours, c === 'K' || n >= 2 ? 2 : 1)}${c === 'K' || n === 3 ? ':' + pad(offsetMinutes) : ''}`
      : c === '/' ? settings.dateSeparator : ':';
    if (c === 'F' && !value && text.endsWith('.')) text = text.slice(0, -1);
    text += value;
  }
  return { text, warnings };
}
