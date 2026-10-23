import { getJsonSyntaxDiagnostic } from '@/components/CodeEditor/JsonEditorPane';

describe('JSON editor diagnostics', () => {
  test('locates the first syntax error for the Monaco gutter marker', () => {
    const diagnostic = getJsonSyntaxDiagnostic('{\n  "pages": [],\n  "name": invalid\n}');

    expect(diagnostic).toMatchObject({ line: 3, column: 11 });
    expect(diagnostic?.message).toBeTruthy();
  });

  test('does not create a marker for valid JSON', () => {
    expect(getJsonSyntaxDiagnostic('{"pages":[]}')).toBeNull();
  });
});
