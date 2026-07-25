/**
 * @jest-environment jsdom
 */
import { sanitizeRichTextHtml } from '@/utils/sanitizeRichTextHtml';

describe('sanitizeRichTextHtml', () => {
  test.each([
    'javascript:alert%281%29',
    'data:text/html;base64,PHNjcmlwdD4=',
    'file:///etc/passwd',
    'vbscript:msgbox%281%29',
  ])('removes unsafe link target %s', target => {
    const result = sanitizeRichTextHtml(`<p><a href="${target}">open</a></p>`);

    expect(result).toContain('<a>open</a>');
    expect(result).not.toContain(target);
  });

  test('removes scripts, event handlers, and unsafe style properties', () => {
    const result = sanitizeRichTextHtml(
      '<p onclick="alert(1)" style="color:red;position:fixed">Safe<script>alert(1)</script></p>',
    );

    expect(result).toContain('style="color: red;"');
    expect(result).not.toContain('onclick');
    expect(result).not.toContain('position');
    expect(result).not.toContain('script');
  });

  test('preserves formatting and safe links', () => {
    const result = sanitizeRichTextHtml(
      '<p><strong>Bold</strong> <a href="https://example.com" target="_blank">docs</a></p>',
    );

    expect(result).toContain('<strong>Bold</strong>');
    expect(result).toContain('href="https://example.com"');
    expect(result).toContain('rel="noopener noreferrer"');
  });

  test('preserves existing RTL rich-text containers', () => {
    const result = sanitizeRichTextHtml(
      '<div style="text-align:right; direction:rtl"><p><strong>مرحبا</strong></p></div>',
    );

    expect(result).toContain('text-align:right');
    expect(result).toContain('direction:rtl');
    expect(result).toContain('<p><strong>مرحبا</strong></p>');
  });
});
