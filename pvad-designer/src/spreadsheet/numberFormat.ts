// Apply an Excel-style number-format code to a value for on-screen display, so the grid matches Excel and
// the exported file. Covers the toolbar presets (number / currency / percent / date) and degrades
// gracefully (returns the raw string) for anything it doesn't recognize.

const DATE_TOKENS = /dd|MM|yyyy|yy|HH|mm|ss/g;

function formatDate(d: Date, fmt: string): string {
  const pad = (x: number) => String(x).padStart(2, '0');
  return fmt.replace(DATE_TOKENS, (t) => {
    switch (t) {
      case 'dd': return pad(d.getDate());
      case 'MM': return pad(d.getMonth() + 1);
      case 'yyyy': return String(d.getFullYear());
      case 'yy': return pad(d.getFullYear() % 100);
      case 'HH': return pad(d.getHours());
      case 'mm': return pad(d.getMinutes());
      case 'ss': return pad(d.getSeconds());
      default: return t;
    }
  });
}

export function formatCellValue(value: string | number | boolean | null, fmt?: string): string {
  if (value == null) return '';
  if (!fmt) return String(value);

  // Date format: contains date tokens and no numeric placeholders.
  if (/[dyHs]/.test(fmt) && !/[#0]/.test(fmt)) {
    const d = new Date(String(value)); // dates in the model are ISO strings
    return Number.isNaN(d.getTime()) ? String(value) : formatDate(d, fmt);
  }

  const n = typeof value === 'number' ? value : Number(value);
  if (typeof value === 'boolean' || Number.isNaN(n)) return String(value);

  // Percent, e.g. "0.00%".
  if (fmt.includes('%')) {
    const dec = fmt.match(/0\.(0+)\s*%/)?.[1].length ?? 0;
    return `${(n * 100).toFixed(dec)}%`;
  }

  // Number / currency, e.g. "#,##0.00" or "\"€\"#,##0.00".
  const dec = fmt.match(/\.(0+)/)?.[1].length ?? 0;
  const grouped = fmt.includes(',');
  const num = n.toLocaleString('en-US', { minimumFractionDigits: dec, maximumFractionDigits: dec, useGrouping: grouped });
  const sym = fmt.match(/^"([^"]+)"|^([^\s#0,.%-]+)/); // leading currency symbol, quoted or bare
  const prefix = sym ? (sym[1] ?? sym[2] ?? '') : '';
  return prefix + num;
}
