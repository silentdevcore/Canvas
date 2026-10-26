/** @jest-environment jsdom */
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { act, createElement } from 'react';
import { createRoot } from 'react-dom/client';
import * as icons from 'react-icons/fi';
import ts from 'typescript';

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean })
  .IS_REACT_ACT_ENVIRONMENT = true;

function usedIcons(directory: string): string[] {
  const names = new Set<string>();
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (entry.name === '__tests__') continue;
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      usedIcons(file).forEach(name => names.add(name));
    } else if (entry.name.endsWith('.ts') || entry.name.endsWith('.tsx')) {
      const source = ts.createSourceFile(file, readFileSync(file, 'utf8'), ts.ScriptTarget.Latest, true);
      for (const statement of source.statements) {
        if (!ts.isImportDeclaration(statement) || !ts.isStringLiteral(statement.moduleSpecifier)) continue;
        const module = statement.moduleSpecifier.text;
        if (module !== 'react-icons' && !module.startsWith('react-icons/')) continue;
        expect(module).toBe('react-icons/fi');
        const bindings = statement.importClause?.namedBindings;
        expect(bindings && ts.isNamedImports(bindings)).toBe(true);
        if (bindings && ts.isNamedImports(bindings)) {
          bindings.elements.forEach(item => names.add((item.propertyName ?? item.name).text));
        }
      }
    }
  }
  return [...names].sort();
}

describe('Designer icon compatibility', () => {
  const names = usedIcons(path.resolve(__dirname, '..'));

  test('every source import is a callable Feather icon', () => {
    expect(names.length).toBeGreaterThan(0);
    for (const name of names) {
      expect(typeof icons[name as keyof typeof icons]).toBe('function');
    }
  });

  test('all used icons retain geometry, sizing, color and accessible titles', () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);
    const signatures: string[] = [];
    try {
      for (const name of names) {
        act(() => root.render(createElement(icons[name as keyof typeof icons], {
          size: 20,
          color: '#125a70',
          title: name,
          role: 'img',
          'aria-label': name,
        })));
        const svg = container.querySelector('svg');
        expect(svg).not.toBeNull();
        expect(svg?.namespaceURI).toBe('http://www.w3.org/2000/svg');
        expect(svg?.getAttribute('viewBox')).toBe('0 0 24 24');
        expect(svg?.getAttribute('width')).toBe('20');
        expect(svg?.getAttribute('height')).toBe('20');
        expect(svg?.getAttribute('stroke')).toBe('currentColor');
        expect(svg?.getAttribute('fill')).toBe('none');
        expect(svg?.style.color).toBe('rgb(18, 90, 112)');
        expect(svg?.getAttribute('role')).toBe('img');
        expect(svg?.getAttribute('aria-label')).toBe(name);
        expect(svg?.querySelector('title')?.textContent).toBe(name);
        expect(svg?.querySelector('path, line, polyline, polygon, rect, circle, ellipse')).not.toBeNull();
        signatures.push(`${name}:${svg?.outerHTML}`);
      }
      // The old-package baseline makes unnoticed icon geometry changes explicit.
      expect(createHash('sha256').update(signatures.join('\n')).digest('hex')).toMatchInlineSnapshot(`"f38366789232f327445fa5d2ad1781e06c250a8947aeb817246c5f7e434b4a5f"`);
    } finally {
      act(() => root.unmount());
      container.remove();
    }
  });
});
