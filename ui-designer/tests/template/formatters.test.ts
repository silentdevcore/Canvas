import { formatValue, getAvailableFormatters, validateFormatterArgs } from '../../src/template/formatters';

describe('Formatters', () => {
  describe('formatValue', () => {
    it('should format currency values', () => {
      expect(formatValue(1234.56, 'currency', { currency: 'USD', locale: 'en-US' })).toBe('$1,234.56');
      expect(formatValue(1234.56, 'currency', { currency: 'EUR', locale: 'de-DE' })).toBe('1.234,56 €');
      expect(formatValue(1234.56, 'currency', { currency: 'JPY', locale: 'ja-JP' })).toBe('￥1,235');
    });

    it('should format date values', () => {
      const date = new Date('2024-01-15T10:30:00Z');
      expect(formatValue(date, 'date', { format: 'MM/dd/yyyy' })).toBe('01/15/2024');
      expect(formatValue(date, 'date', { format: 'yyyy-MM-dd' })).toBe('2024-01-15');
      expect(formatValue(date, 'date', { format: 'MMM dd, yyyy' })).toBe('Jan 15, 2024');
    });

    it('should format date strings', () => {
      expect(formatValue('2024-01-15', 'date', { format: 'MM/dd/yyyy' })).toBe('01/15/2024');
      expect(formatValue('2024-01-15T10:30:00Z', 'date', { format: 'yyyy-MM-dd' })).toBe('2024-01-15');
    });

    it('should format time values', () => {
      const date = new Date('2024-01-15T14:30:45Z');
      expect(formatValue(date, 'time', { format: 'HH:mm:ss' })).toBe('14:30:45');
      expect(formatValue(date, 'time', { format: 'hh:mm a' })).toBe('02:30 PM');
      expect(formatValue(date, 'time', { format: 'HH:mm' })).toBe('14:30');
    });

    it('should format datetime values', () => {
      const date = new Date('2024-01-15T14:30:45Z');
      expect(formatValue(date, 'datetime', { format: 'MM/dd/yyyy HH:mm' })).toBe('01/15/2024 14:30');
      expect(formatValue(date, 'datetime', { format: 'yyyy-MM-dd HH:mm:ss' })).toBe('2024-01-15 14:30:45');
    });

    it('should format numbers with precision', () => {
      expect(formatValue(1234.56789, 'number', { precision: 2, locale: 'en-US' })).toBe('1,234.57');
      expect(formatValue(1234.56789, 'number', { precision: 0, locale: 'en-US' })).toBe('1,235');
      expect(formatValue(1234.56789, 'number', { precision: 4, locale: 'en-US' })).toBe('1,234.5679');
    });

    it('should format percentages', () => {
      expect(formatValue(0.1234, 'percentage', { precision: 2, locale: 'en-US' })).toBe('12.34%');
      expect(formatValue(0.5, 'percentage', { precision: 0, locale: 'en-US' })).toBe('50%');
    });

    it('should format text transformations', () => {
      expect(formatValue('hello world', 'uppercase')).toBe('HELLO WORLD');
      expect(formatValue('HELLO WORLD', 'lowercase')).toBe('hello world');
      expect(formatValue('hello world', 'capitalize')).toBe('Hello World');
      expect(formatValue('hello world', 'titlecase')).toBe('Hello World');
    });

    it('should format text truncation', () => {
      expect(formatValue('This is a long text', 'truncate', { length: 10 })).toBe('This is a...');
      expect(formatValue('Short', 'truncate', { length: 10 })).toBe('Short');
      expect(formatValue('This is a long text', 'truncate', { length: 10, suffix: '***' })).toBe('This is a***');
    });

    it('should format phone numbers', () => {
      expect(formatValue('1234567890', 'phone', { format: 'US' })).toBe('(123) 456-7890');
      expect(formatValue('+33123456789', 'phone', { format: 'FR' })).toBe('+33 1 23 45 67 89');
    });

    it('should format file sizes', () => {
      expect(formatValue(1024, 'filesize')).toBe('1.00 KB');
      expect(formatValue(1048576, 'filesize')).toBe('1.00 MB');
      expect(formatValue(1073741824, 'filesize')).toBe('1.00 GB');
    });

    it('should handle null and undefined values', () => {
      expect(formatValue(null, 'currency', { currency: 'USD' })).toBe('');
      expect(formatValue(undefined, 'date', { format: 'MM/dd/yyyy' })).toBe('');
      expect(formatValue(null, 'uppercase')).toBe('');
    });

    it('should handle invalid inputs gracefully', () => {
      expect(formatValue('not-a-date', 'date', { format: 'MM/dd/yyyy' })).toBe('not-a-date');
      expect(formatValue('not-a-number', 'currency', { currency: 'USD' })).toBe('not-a-number');
      expect(formatValue({}, 'uppercase')).toBe('[object Object]');
    });

    it('should support custom formatters', () => {
      // Test custom formatter function
      const customFormatter = (value: any, options: any) => {
        return `Custom: ${value} with ${options?.param || 'default'}`;
      };

      expect(formatValue('test', customFormatter, { param: 'option' })).toBe('Custom: test with option');
      expect(formatValue('test', customFormatter)).toBe('Custom: test with default');
    });
  });

  describe('getAvailableFormatters', () => {
    it('should return all available formatters', () => {
      const formatters = getAvailableFormatters();

      expect(formatters).toBeDefined();
      expect(Array.isArray(formatters)).toBe(true);
      expect(formatters.length).toBeGreaterThan(0);

      // Check that each formatter has required properties
      formatters.forEach(formatter => {
        expect(formatter).toHaveProperty('name');
        expect(formatter).toHaveProperty('description');
        expect(formatter).toHaveProperty('category');
        expect(formatter).toHaveProperty('examples');
      });

      // Check for specific formatters
      const currencyFormatter = formatters.find(f => f.name === 'currency');
      expect(currencyFormatter).toBeDefined();
      expect(currencyFormatter!.category).toBe('number');

      const dateFormatter = formatters.find(f => f.name === 'date');
      expect(dateFormatter).toBeDefined();
      expect(dateFormatter!.category).toBe('date');
    });

    it('should categorize formatters correctly', () => {
      const formatters = getAvailableFormatters();

      const categories = [...new Set(formatters.map(f => f.category))];
      expect(categories).toContain('number');
      expect(categories).toContain('date');
      expect(categories).toContain('text');
    });
  });

  describe('validateFormatterArgs', () => {
    it('should validate currency formatter arguments', () => {
      expect(validateFormatterArgs('currency', { currency: 'USD', locale: 'en-US' })).toBe(true);
      expect(validateFormatterArgs('currency', { currency: 'INVALID' })).toBe(false);
      expect(validateFormatterArgs('currency', {})).toBe(false);
    });

    it('should validate date formatter arguments', () => {
      expect(validateFormatterArgs('date', { format: 'MM/dd/yyyy' })).toBe(true);
      expect(validateFormatterArgs('date', { format: '' })).toBe(false);
      expect(validateFormatterArgs('date', {})).toBe(false);
    });

    it('should validate number formatter arguments', () => {
      expect(validateFormatterArgs('number', { precision: 2, locale: 'en-US' })).toBe(true);
      expect(validateFormatterArgs('number', { precision: -1 })).toBe(false);
      expect(validateFormatterArgs('number', { precision: 'invalid' })).toBe(false);
    });

    it('should validate truncate formatter arguments', () => {
      expect(validateFormatterArgs('truncate', { length: 10 })).toBe(true);
      expect(validateFormatterArgs('truncate', { length: 0 })).toBe(false);
      expect(validateFormatterArgs('truncate', { length: -5 })).toBe(false);
    });

    it('should validate phone formatter arguments', () => {
      expect(validateFormatterArgs('phone', { format: 'US' })).toBe(true);
      expect(validateFormatterArgs('phone', { format: 'INVALID' })).toBe(false);
      expect(validateFormatterArgs('phone', {})).toBe(false);
    });

    it('should return true for formatters without required arguments', () => {
      expect(validateFormatterArgs('uppercase', {})).toBe(true);
      expect(validateFormatterArgs('lowercase', {})).toBe(true);
      expect(validateFormatterArgs('capitalize', {})).toBe(true);
    });

    it('should handle unknown formatters', () => {
      expect(validateFormatterArgs('unknown-formatter', {})).toBe(false);
    });
  });

  describe('formatter pipeline', () => {
    it('should support chaining multiple formatters', () => {
      // This would be a more advanced feature - for now just test individual formatters
      const value = 'hello world';

      const uppercased = formatValue(value, 'uppercase');
      expect(uppercased).toBe('HELLO WORLD');

      const truncated = formatValue(uppercased, 'truncate', { length: 8 });
      expect(truncated).toBe('HELLO WO...');
    });

    it('should handle locale-specific formatting', () => {
      // Test with different locales
      expect(formatValue(1234.56, 'currency', { currency: 'USD', locale: 'en-US' })).toBe('$1,234.56');
      expect(formatValue(1234.56, 'currency', { currency: 'EUR', locale: 'de-DE' })).toBe('1.234,56 €');
      expect(formatValue(1234.56, 'number', { locale: 'de-DE' })).toBe('1.234,56');
    });
  });

  describe('error handling', () => {
    it('should handle invalid formatter names gracefully', () => {
      expect(() => formatValue('test', 'invalid-formatter' as any)).not.toThrow();
      expect(formatValue('test', 'invalid-formatter' as any)).toBe('test');
    });

    it('should handle malformed options gracefully', () => {
      expect(() => formatValue('test', 'truncate', null as any)).not.toThrow();
      expect(() => formatValue('test', 'truncate', undefined as any)).not.toThrow();
    });

    it('should handle edge cases in input values', () => {
      expect(formatValue('', 'uppercase')).toBe('');
      expect(formatValue(0, 'currency', { currency: 'USD' })).toBe('$0.00');
      expect(formatValue(NaN, 'number')).toBe('NaN');
      expect(formatValue(Infinity, 'number')).toBe('∞');
    });
  });
});