/**
 * Exact decimal arithmetic for invoice amounts.
 *
 * Invoice totals have to agree with the backend to the last cent, and the backend computes them
 * with .NET `decimal` and `MidpointRounding.AwayFromZero`. IEEE-754 doubles cannot reproduce that
 * (0.1 + 0.2 !== 0.3, and `Math.round` rounds half *up*, not half *away from zero*), so every
 * amount is carried here as a scaled `bigint`: an integer mantissa plus a decimal scale.
 *
 * A JavaScript number enters through its shortest round-trip decimal text, which is exactly the
 * JSON literal the backend deserializes into a `decimal`. That makes the two sides start from
 * identical values rather than merely similar ones.
 */

export interface ExactDecimal {
  /** Unscaled integer value: the real value is `units / 10 ** scale`. */
  readonly units: bigint;
  /** Number of decimal places, always >= 0. */
  readonly scale: number;
}

/** Highest scale accepted from input, mirroring the 28-29 significant digits of .NET decimal. */
const MAXIMUM_SCALE = 28;

export const ZERO: ExactDecimal = { units: 0n, scale: 0 };

export function fromNumber(value: number): ExactDecimal {
  if (!Number.isFinite(value)) {
    throw new RangeError('An invoice amount must be a finite number.');
  }
  // `String(value)` is the shortest representation that round-trips, i.e. the same text the
  // backend receives in JSON.
  return fromString(String(value));
}

export function fromString(text: string): ExactDecimal {
  const trimmed = text.trim();
  const match = /^([+-]?)(\d*)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$/.exec(trimmed);
  if (!match || (match[2] === '' && (match[3] ?? '') === '')) {
    throw new RangeError(`'${text}' is not a decimal number.`);
  }

  const [, sign, whole, fraction = '', exponent] = match;
  let units = BigInt((whole || '0') + fraction);
  let scale = fraction.length;
  if (exponent) {
    const shift = Number(exponent);
    if (shift > 0) {
      // Multiplying by a power of ten first consumes the existing scale.
      const consumed = Math.min(shift, scale);
      scale -= consumed;
      units *= 10n ** BigInt(shift - consumed);
    } else {
      scale += -shift;
    }
  }
  if (scale > MAXIMUM_SCALE) {
    throw new RangeError(`'${text}' has more than ${MAXIMUM_SCALE} decimal places.`);
  }

  return normalize({ units: sign === '-' ? -units : units, scale });
}

export function fromValue(value: number | string | ExactDecimal): ExactDecimal {
  if (typeof value === 'number') return fromNumber(value);
  if (typeof value === 'string') return fromString(value);
  return value;
}

/** Drops trailing zeros so equal values always share one representation. */
function normalize(value: ExactDecimal): ExactDecimal {
  let { units, scale } = value;
  while (scale > 0 && units % 10n === 0n) {
    units /= 10n;
    scale -= 1;
  }
  return { units, scale };
}

function rescale(value: ExactDecimal, scale: number): bigint {
  if (scale < value.scale) {
    throw new RangeError('Rescaling to a smaller scale would lose digits; round instead.');
  }
  return value.units * 10n ** BigInt(scale - value.scale);
}

export function add(left: ExactDecimal, right: ExactDecimal): ExactDecimal {
  const scale = Math.max(left.scale, right.scale);
  return normalize({ units: rescale(left, scale) + rescale(right, scale), scale });
}

export function sum(values: readonly ExactDecimal[]): ExactDecimal {
  return values.reduce(add, ZERO);
}

export function multiply(left: ExactDecimal, right: ExactDecimal): ExactDecimal {
  return normalize({ units: left.units * right.units, scale: left.scale + right.scale });
}

export function compare(left: ExactDecimal, right: ExactDecimal): number {
  const scale = Math.max(left.scale, right.scale);
  const a = rescale(left, scale);
  const b = rescale(right, scale);
  return a < b ? -1 : a > b ? 1 : 0;
}

export function isZero(value: ExactDecimal): boolean {
  return value.units === 0n;
}

export function isNegative(value: ExactDecimal): boolean {
  return value.units < 0n;
}

/**
 * Rounds half away from zero, matching `Math.Round(value, places, MidpointRounding.AwayFromZero)`.
 * Half *up* would send -0.125 to -0.12 where the backend produces -0.13.
 */
export function round(value: ExactDecimal, decimalPlaces: number): ExactDecimal {
  if (!Number.isInteger(decimalPlaces) || decimalPlaces < 0 || decimalPlaces > MAXIMUM_SCALE) {
    throw new RangeError('Decimal places must be an integer between 0 and 28.');
  }
  if (value.scale <= decimalPlaces) {
    return normalize(value);
  }

  const divisor = 10n ** BigInt(value.scale - decimalPlaces);
  const negative = value.units < 0n;
  const magnitude = negative ? -value.units : value.units;
  const quotient = magnitude / divisor;
  const remainder = magnitude % divisor;
  const rounded = remainder * 2n >= divisor ? quotient + 1n : quotient;
  return normalize({ units: negative ? -rounded : rounded, scale: decimalPlaces });
}

/** Fixed-point text with exactly `decimalPlaces` digits, as the backend's "F2" format produces. */
export function toFixed(value: ExactDecimal, decimalPlaces: number): string {
  const rounded = round(value, decimalPlaces);
  const units = rescale(rounded, decimalPlaces);
  const negative = units < 0n;
  const digits = (negative ? -units : units).toString().padStart(decimalPlaces + 1, '0');
  const whole = digits.slice(0, digits.length - decimalPlaces);
  const fraction = decimalPlaces === 0 ? '' : `.${digits.slice(digits.length - decimalPlaces)}`;
  return `${negative ? '-' : ''}${whole}${fraction}`;
}

/**
 * Shortest text for the value, matching the backend's "0.############################" format
 * used for quantities and tax rates.
 */
export function toPlainString(value: ExactDecimal): string {
  const normalized = normalize(value);
  if (normalized.scale === 0) return normalized.units.toString();
  return toFixed(normalized, normalized.scale);
}

/**
 * Only for display and chart input. Never feed the result back into arithmetic: that is what
 * loses the exactness this module exists to preserve.
 */
export function toNumber(value: ExactDecimal): number {
  return Number(toPlainString(value));
}
