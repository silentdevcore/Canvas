/**
 * Data formatting engine for template value formatting
 * Provides comprehensive formatting for numbers, dates, currencies, and text
 */

export interface FormatterConfig {
  type: 'number' | 'currency' | 'date' | 'datetime' | 'percentage' | 'text' | 'custom';
  locale?: string;
  options?: Record<string, any>;
  pattern?: string; // For custom formatting
  fallback?: string;
}

export interface FormatPipeline {
  formatters: FormatterConfig[];
  separator?: string; // For joining multiple formatters
}

export interface FormatResult {
  value: string;
  isValid: boolean;
  error?: string;
}

/**
 * Comprehensive formatter registry
 */
const formatters = {
  number: (value: any, config: FormatterConfig): FormatResult => {
    const num = Number(value);
    if (isNaN(num)) {
      return { value: config.fallback || '', isValid: false, error: 'Invalid number' };
    }

    try {
      const options = {
        minimumFractionDigits: config.options?.minimumFractionDigits || 0,
        maximumFractionDigits: config.options?.maximumFractionDigits || 2,
        useGrouping: config.options?.useGrouping !== false,
        ...config.options
      };

      const formatted = new Intl.NumberFormat(config.locale || 'en-US', options).format(num);
      return { value: formatted, isValid: true };
    } catch (error) {
      return { value: config.fallback || String(num), isValid: false, error: 'Formatting error' };
    }
  },

  currency: (value: any, config: FormatterConfig): FormatResult => {
    const num = Number(value);
    if (isNaN(num)) {
      return { value: config.fallback || '', isValid: false, error: 'Invalid currency amount' };
    }

    try {
      const options = {
        style: 'currency' as const,
        currency: config.options?.currency || 'USD',
        minimumFractionDigits: config.options?.minimumFractionDigits || 2,
        maximumFractionDigits: config.options?.maximumFractionDigits || 2,
        ...config.options
      };

      const formatted = new Intl.NumberFormat(config.locale || 'en-US', options).format(num);
      return { value: formatted, isValid: true };
    } catch (error) {
      return { value: config.fallback || String(num), isValid: false, error: 'Currency formatting error' };
    }
  },

  date: (value: any, config: FormatterConfig): FormatResult => {
    const date = new Date(value);
    if (isNaN(date.getTime())) {
      return { value: config.fallback || '', isValid: false, error: 'Invalid date' };
    }

    try {
      const options = {
        year: 'numeric' as const,
        month: 'short' as const,
        day: 'numeric' as const,
        ...config.options
      };

      const formatted = new Intl.DateTimeFormat(config.locale || 'en-US', options).format(date);
      return { value: formatted, isValid: true };
    } catch (error) {
      return { value: config.fallback || date.toLocaleDateString(), isValid: false, error: 'Date formatting error' };
    }
  },

  datetime: (value: any, config: FormatterConfig): FormatResult => {
    const date = new Date(value);
    if (isNaN(date.getTime())) {
      return { value: config.fallback || '', isValid: false, error: 'Invalid datetime' };
    }

    try {
      const options = {
        year: 'numeric' as const,
        month: 'short' as const,
        day: 'numeric' as const,
        hour: '2-digit' as const,
        minute: '2-digit' as const,
        ...config.options
      };

      const formatted = new Intl.DateTimeFormat(config.locale || 'en-US', options).format(date);
      return { value: formatted, isValid: true };
    } catch (error) {
      return { value: config.fallback || date.toLocaleString(), isValid: false, error: 'Datetime formatting error' };
    }
  },

  percentage: (value: any, config: FormatterConfig): FormatResult => {
    const num = Number(value);
    if (isNaN(num)) {
      return { value: config.fallback || '', isValid: false, error: 'Invalid percentage' };
    }

    try {
      const options = {
        style: 'percent' as const,
        minimumFractionDigits: config.options?.minimumFractionDigits || 0,
        maximumFractionDigits: config.options?.maximumFractionDigits || 2,
        ...config.options
      };

      const formatted = new Intl.NumberFormat(config.locale || 'en-US', options).format(num);
      return { value: formatted, isValid: true };
    } catch (error) {
      return { value: config.fallback || `${num}%`, isValid: false, error: 'Percentage formatting error' };
    }
  },

  text: (value: any, config: FormatterConfig): FormatResult => {
    if (value == null) {
      return { value: config.fallback || '', isValid: false, error: 'Null or undefined value' };
    }

    const text = String(value);

    try {
      let formatted = text;

      // Apply text transformations
      if (config.options?.uppercase) formatted = formatted.toUpperCase();
      if (config.options?.lowercase) formatted = formatted.toLowerCase();
      if (config.options?.capitalize) {
        formatted = formatted.charAt(0).toUpperCase() + formatted.slice(1).toLowerCase();
      }
      if (config.options?.titleCase) {
        formatted = formatted.replace(/\w\S*/g, (txt) =>
          txt.charAt(0).toUpperCase() + txt.substr(1).toLowerCase()
        );
      }

      // Apply length limits
      if (config.options?.maxLength && formatted.length > config.options.maxLength) {
        formatted = formatted.substring(0, config.options.maxLength);
        if (config.options?.ellipsis) {
          formatted += '...';
        }
      }

      // Apply prefix/suffix
      if (config.options?.prefix) formatted = config.options.prefix + formatted;
      if (config.options?.suffix) formatted = formatted + config.options.suffix;

      return { value: formatted, isValid: true };
    } catch (error) {
      return { value: config.fallback || text, isValid: false, error: 'Text formatting error' };
    }
  },

  custom: (value: any, config: FormatterConfig): FormatResult => {
    if (!config.pattern) {
      return { value: config.fallback || String(value), isValid: false, error: 'No custom pattern provided' };
    }

    try {
      let formatted = config.pattern;

      // Simple placeholder replacement
      formatted = formatted.replace(/\{\{value\}\}/g, String(value));

      // More advanced replacements could be added here
      // For example: {{value:currency}}, {{value:date}}, etc.

      return { value: formatted, isValid: true };
    } catch (error) {
      return { value: config.fallback || String(value), isValid: false, error: 'Custom formatting error' };
    }
  }
};

/**
 * Applies a single formatter to a value
 */
export function applyFormatter(value: any, config: FormatterConfig): FormatResult {
  const formatter = formatters[config.type];
  if (!formatter) {
    return { value: config.fallback || String(value), isValid: false, error: `Unknown formatter type: ${config.type}` };
  }

  return formatter(value, config);
}

/**
 * Applies a formatting pipeline to a value
 */
export function applyFormatPipeline(value: any, pipeline: FormatPipeline): FormatResult {
  if (!pipeline.formatters || pipeline.formatters.length === 0) {
    return { value: String(value), isValid: true };
  }

  const results: string[] = [];
  let hasError = false;
  let errorMessage = '';

  for (const formatter of pipeline.formatters) {
    const result = applyFormatter(value, formatter);
    results.push(result.value);

    if (!result.isValid) {
      hasError = true;
      errorMessage = result.error || 'Formatting error';
      // Continue processing other formatters even if one fails
    }
  }

  const finalValue = results.join(pipeline.separator || ' ');
  return {
    value: finalValue,
    isValid: !hasError,
    error: hasError ? errorMessage : undefined
  };
}

/**
 * Validates a formatter configuration
 */
export function validateFormatterConfig(config: FormatterConfig): { isValid: boolean; errors: string[] } {
  const errors: string[] = [];

  if (!config.type) {
    errors.push('Formatter type is required');
  } else if (!formatters[config.type]) {
    errors.push(`Unknown formatter type: ${config.type}`);
  }

  if (config.type === 'custom' && !config.pattern) {
    errors.push('Custom formatter requires a pattern');
  }

  if (config.type === 'currency' && !config.options?.currency) {
    errors.push('Currency formatter requires currency option');
  }

  return {
    isValid: errors.length === 0,
    errors
  };
}

/**
 * Gets available formatter types with descriptions
 */
export function getFormatterTypes(): Array<{ type: string; label: string; description: string }> {
  return [
    { type: 'number', label: 'Number', description: 'Format numbers with decimal places and grouping' },
    { type: 'currency', label: 'Currency', description: 'Format monetary values with currency symbols' },
    { type: 'date', label: 'Date', description: 'Format dates in localized formats' },
    { type: 'datetime', label: 'Date & Time', description: 'Format dates and times together' },
    { type: 'percentage', label: 'Percentage', description: 'Format decimal values as percentages' },
    { type: 'text', label: 'Text', description: 'Transform and format text content' },
    { type: 'custom', label: 'Custom', description: 'Apply custom formatting patterns' }
  ];
}

/**
 * Gets default options for a formatter type
 */
export function getDefaultFormatterOptions(type: string): Record<string, any> {
  const defaults: Record<string, Record<string, any>> = {
    number: {
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
      useGrouping: true
    },
    currency: {
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    },
    date: {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    },
    datetime: {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    },
    percentage: {
      minimumFractionDigits: 0,
      maximumFractionDigits: 2
    },
    text: {
      uppercase: false,
      lowercase: false,
      capitalize: false,
      titleCase: false,
      maxLength: undefined,
      ellipsis: false
    },
    custom: {
      pattern: '{{value}}'
    }
  };

  return defaults[type] || {};
}

/**
 * Creates a common formatters library
 */
export const commonFormatters: Record<string, FormatterConfig> = {
  currencyUSD: {
    type: 'currency',
    locale: 'en-US',
    options: { currency: 'USD' }
  },
  currencyEUR: {
    type: 'currency',
    locale: 'en-EU',
    options: { currency: 'EUR' }
  },
  dateShort: {
    type: 'date',
    options: { year: 'numeric', month: 'short', day: 'numeric' }
  },
  dateLong: {
    type: 'date',
    options: { year: 'numeric', month: 'long', day: 'numeric' }
  },
  datetimeShort: {
    type: 'datetime',
    options: { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }
  },
  numberInteger: {
    type: 'number',
    options: { minimumFractionDigits: 0, maximumFractionDigits: 0 }
  },
  numberDecimal: {
    type: 'number',
    options: { minimumFractionDigits: 2, maximumFractionDigits: 2 }
  },
  percentage: {
    type: 'percentage',
    options: { minimumFractionDigits: 1, maximumFractionDigits: 1 }
  },
  textUppercase: {
    type: 'text',
    options: { uppercase: true }
  },
  textTitleCase: {
    type: 'text',
    options: { titleCase: true }
  }
};