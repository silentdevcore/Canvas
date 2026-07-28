import fs from 'node:fs';
import path from 'node:path';
import { applyVerticalWheelToHorizontalScroll } from '@/utils/editorScrolling';

describe('editor scrollable workspace', () => {
  test('maps a vertical wheel gesture to horizontal page-strip movement', () => {
    const strip = { clientWidth: 400, scrollWidth: 900, scrollLeft: 120 };

    expect(applyVerticalWheelToHorizontalScroll(strip, 0, 80)).toBe(true);
    expect(strip.scrollLeft).toBe(200);
  });

  test('leaves native gestures alone when horizontal scrolling is unavailable or explicit', () => {
    const fittingStrip = { clientWidth: 400, scrollWidth: 400, scrollLeft: 0 };
    const overflowingStrip = { clientWidth: 400, scrollWidth: 900, scrollLeft: 120 };

    expect(applyVerticalWheelToHorizontalScroll(fittingStrip, 0, 80)).toBe(false);
    expect(applyVerticalWheelToHorizontalScroll(overflowingStrip, 80, 20)).toBe(false);
    expect(overflowingStrip.scrollLeft).toBe(120);
  });

  test('sizes desktop panels to the complete editor stage and keeps navigation surfaces scrollable', () => {
    const css = fs.readFileSync(
      path.join(process.cwd(), 'src/styles/editor.css'),
      'utf8',
    );
    const source = fs.readFileSync(
      path.join(process.cwd(), 'src/components/Editor/SimplePxaSurface.tsx'),
      'utf8',
    );

    expect(css).toMatch(/\.editor-panel\s*\{[^}]*height:\s*var\(--editor-stage-height,\s*auto\);/s);
    expect(css).toMatch(/\.editor-panel\s*\{[^}]*overflow:\s*auto;/s);
    expect(css).toMatch(/\.editor-panel\s*\{[^}]*min-height:\s*0;/s);
    expect(css).not.toMatch(/\.editor-panel\s*\{[^}]*position:\s*sticky;/s);
    expect(css).toMatch(/\.editor-page-viewport\s*\{[^}]*overflow-x:\s*scroll;/s);
    expect(css).toMatch(/\.editor-page-strip\s*\{[^}]*overflow-x:\s*scroll;/s);
    expect(css).toContain('.editor-persistent-scrollbar');
    expect(css).toContain('.editor-persistent-scrollbar input::-webkit-slider-thumb');
    expect(css).toMatch(/\.editor-page-thumb\s*\{[^}]*flex:\s*0 0 auto;/s);
    expect(source).toContain('className="editor-page-scale-frame"');
    expect(source).toContain("'--editor-stage-height'");
    expect(source).toContain('new ResizeObserver(syncPanelHeight)');
    expect(source).toContain('observer.observe(stage)');
    expect(source).toContain("transformOrigin: 'top left'");
    expect(source).toContain("activePageThumbRef.current?.scrollIntoView");
    expect(source).toContain('<PersistentHorizontalScrollbar');
  });
});
