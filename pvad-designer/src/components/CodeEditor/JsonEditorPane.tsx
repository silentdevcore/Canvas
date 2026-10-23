import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Editor, { type Monaco, type OnMount } from '@monaco-editor/react';
import { STARTER_TEMPLATES, STARTER_LABELS, type StarterKey } from './starterTemplates';
import type { EditorLanguage } from './LiveCodeEditor';

interface Props {
  value: string;
  language: EditorLanguage;
  onChange: (value: string) => void;
  onCsharpConvert?: (code: string) => Promise<void>;
}

const DEBOUNCE_MS = 400;

export interface JsonSyntaxDiagnostic {
  message: string;
  line: number;
  column: number;
}

function positionToLocation(raw: string, position: number) {
  const beforeError = raw.slice(0, Math.max(0, position));
  const lines = beforeError.split(/\r?\n/);
  return { line: lines.length, column: (lines[lines.length - 1]?.length ?? 0) + 1 };
}

function findUnexpectedToken(raw: string, token: string) {
  let inString = false;
  let escaped = false;
  for (let index = 0; index < raw.length; index++) {
    const character = raw[index];
    if (inString) {
      if (escaped) escaped = false;
      else if (character === '\\') escaped = true;
      else if (character === '"') inString = false;
    } else if (character === '"') inString = true;
    else if (character === token) return index;
  }
  return 0;
}

export function getJsonSyntaxDiagnostic(raw: string): JsonSyntaxDiagnostic | null {
  try {
    JSON.parse(raw);
    return null;
  } catch (cause) {
    const message = cause instanceof Error ? cause.message : String(cause);
    const explicitLocation = message.match(/line\s+(\d+)\s+column\s+(\d+)/i);
    if (explicitLocation) {
      return { message, line: Number(explicitLocation[1]), column: Number(explicitLocation[2]) };
    }
    const positionMatch = message.match(/position\s+(\d+)/i);
    const unexpectedToken = message.match(/Unexpected token ['"](.{1})['"]/i)?.[1];
    const position = positionMatch
      ? Number(positionMatch[1])
      : unexpectedToken
        ? findUnexpectedToken(raw, unexpectedToken)
        : 0;
    return { message, ...positionToLocation(raw, position) };
  }
}

function updateJsonMarker(monaco: Monaco, model: any, raw: string) {
  const diagnostic = getJsonSyntaxDiagnostic(raw);
  monaco.editor.setModelMarkers(model, 'pvad-json', diagnostic ? [{
    startLineNumber: diagnostic.line,
    startColumn: diagnostic.column,
    endLineNumber: diagnostic.line,
    endColumn: diagnostic.column + 1,
    message: diagnostic.message,
    severity: monaco.MarkerSeverity.Error,
  }] : []);
}

export default function JsonEditorPane({ value, language, onChange, onCsharpConvert }: Props) {
  const { t } = useTranslation('codeEditor');
  const editorRef = useRef<any>(null);
  const monacoRef = useRef<Monaco | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [copied, setCopied] = useState(false);
  const [isRunning, setIsRunning] = useState(false);
  const [statistics, setStatistics] = useState(() => ({ lines: value.split(/\r?\n/).length, characters: value.length }));

  const handleMount: OnMount = (editor, monaco) => {
    editorRef.current = editor;
    monacoRef.current = monaco;
    if (language === 'json') configureSchema(monaco); // JSON schema only in JSON mode
    if (language === 'json') updateJsonMarker(monaco, editor.getModel(), editor.getValue());

    // Cmd/Ctrl+Enter → force immediate emit / convert
    editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter, async () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      const v = editor.getValue();
      onChange(v);
      if (language !== 'json' && onCsharpConvert) {
        setIsRunning(true);
        await onCsharpConvert(v);
        setIsRunning(false);
      }
    });
  };

  const handleChange = useCallback(
    (raw: string | undefined) => {
      const v = raw ?? '';
      setStatistics({ lines: v.split(/\r?\n/).length, characters: v.length });
      if (language === 'json' && monacoRef.current && editorRef.current?.getModel())
        updateJsonMarker(monacoRef.current, editorRef.current.getModel(), v);
      if (timerRef.current) clearTimeout(timerRef.current);
      timerRef.current = setTimeout(() => onChange(v), DEBOUNCE_MS);
    },
    [onChange],
  );

  useEffect(() => () => { if (timerRef.current) clearTimeout(timerRef.current); }, []);

  const handleFormat = () => {
    editorRef.current?.getAction('editor.action.formatDocument')?.run();
  };

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(editorRef.current?.getValue() ?? value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch { /* ignore */ }
  };

  const handleStarter = (key: StarterKey) => {
    const json = JSON.stringify(STARTER_TEMPLATES[key], null, 2);
    editorRef.current?.setValue(json);
    onChange(json);
  };

  const handleRun = async () => {
    if (!onCsharpConvert) return;
    setIsRunning(true);
    await onCsharpConvert(editorRef.current?.getValue() ?? value);
    setIsRunning(false);
  };

  return (
    <div className="code-editor-pane">
      <div className="code-editor-toolbar">
        <span className="code-editor-toolbar-label">
          {language === 'csharpPdf' ? 'C# PDF' : language === 'csharpModel' ? 'C# Model' : language === 'csharpBase64' ? 'FromBase64String' : 'JSON'}
        </span>

        <div className="code-editor-toolbar-group">
          <span className="code-editor-hint">⌘↵ {t(language === 'json' ? 'editor.refresh' : 'editor.run')}</span>
          <button className="code-editor-btn" onClick={handleFormat} title={t('editor.format')}>
            {t('editor.format')}
          </button>
          <button className="code-editor-btn" onClick={handleCopy} title={t('editor.copy')}>
            {copied ? t('editor.copied') : t('editor.copy')}
          </button>
          {language !== 'json' && (
            <button
              className={`code-editor-run-btn${isRunning ? ' is-running' : ''}`}
              onClick={handleRun}
              disabled={isRunning}
              title={t('editor.runTitle')}
            >
              {isRunning ? t('editor.running') : t('editor.run')}
            </button>
          )}
        </div>

        {language === 'json' && (
          <div className="code-editor-starter-group">
            {(Object.keys(STARTER_LABELS) as StarterKey[]).map(key => (
              <button
                key={key}
                className="code-editor-starter-btn"
                onClick={() => handleStarter(key)}
                title={t('editor.loadStarter', { name: STARTER_LABELS[key] })}
              >
                {STARTER_LABELS[key]}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="code-editor-monaco-wrap">
        <Editor
          defaultValue={value}
          language={language === 'json' ? 'json' : 'csharp'}
          theme="vs-dark"
          onMount={handleMount}
          onChange={handleChange}
          options={{
            minimap: { enabled: false },
            fontSize: 13,
            lineHeight: 20,
            wordWrap: 'on',
            scrollBeyondLastLine: false,
            tabSize: 2,
            formatOnPaste: true,
            automaticLayout: true,
            bracketPairColorization: { enabled: true },
            guides: { bracketPairs: true },
          }}
        />
      </div>
      <div className="code-editor-status" role="status">
        <span>{t('editor.lineCount', { count: statistics.lines })}</span>
        <span>{t('editor.characterCount', { count: statistics.characters })}</span>
      </div>
    </div>
  );
}

function configureSchema(monaco: Monaco) {
  monaco.languages.json.jsonDefaults.setDiagnosticsOptions({
    validate: true,
    schemas: [
      {
        uri: 'pvad://design-schema',
        fileMatch: ['*'],
        schema: {
          type: 'object',
          required: ['pages'],
          properties: {
            id:   { type: 'string', description: 'Design ID' },
            name: { type: 'string', description: 'Document name' },
            pageSettings: {
              type: 'object',
              properties: {
                width:       { type: 'number', description: 'Page width in pt (default 595 = A4)' },
                height:      { type: 'number', description: 'Page height in pt (default 842 = A4)' },
                orientation: { type: 'string', enum: ['portrait', 'landscape'] },
                backgroundColor: { type: 'string', description: 'Hex color e.g. #ffffff' },
                margins: {
                  type: 'object',
                  properties: {
                    top: { type: 'number' }, right: { type: 'number' },
                    bottom: { type: 'number' }, left: { type: 'number' },
                  },
                },
              },
            },
            pages: {
              type: 'array',
              description: 'Array of pages',
              items: {
                type: 'object',
                required: ['id', 'elements'],
                properties: {
                  id:       { type: 'string' },
                  elements: { type: 'array', items: { $ref: '#/definitions/element' } },
                },
              },
            },
            sharedElements: {
              type: 'array',
              description: 'Elements rendered on every page',
              items: { $ref: '#/definitions/element' },
            },
          },
          definitions: {
            element: {
              type: 'object',
              required: ['id', 'type', 'x', 'y', 'width', 'height'],
              properties: {
                id:     { type: 'string' },
                type:   { type: 'string', enum: ['text','richtext','image','rect','shape','circle','line','arrow','table','chart','qrcode','barcode','signature','field','checkbox','checkmark','button','dropdown','optionlist','radio','watermark','note','draw','date','highlight','pageboundary','pagenumber','subsection','area'] },
                x:      { type: 'number', description: 'X position from page left (pt)' },
                y:      { type: 'number', description: 'Y position from page top (pt)' },
                width:  { type: 'number' },
                height: { type: 'number' },
                content:     { type: 'string' },
                htmlContent: { type: 'string', description: 'HTML for richtext elements' },
                style: {
                  type: 'object',
                  properties: {
                    fontSize:        { type: 'number' },
                    fontWeight:      { type: 'string', enum: ['normal','bold','600','700'] },
                    fontStyle:       { type: 'string', enum: ['normal','italic'] },
                    textDecoration:  { type: 'string', enum: ['none','underline','line-through'] },
                    textAlign:       { type: 'string', enum: ['left','center','right','justify'] },
                    color:           { type: 'string' },
                    backgroundColor: { type: 'string' },
                    lineHeight:      { type: 'number', description: 'CSS multiplier e.g. 1.4' },
                    letterSpacing:   { type: 'number' },
                    borderWidth:     { type: 'number' },
                    borderColor:     { type: 'string' },
                    borderStyle:     { type: 'string', enum: ['solid','dashed','dotted','none'] },
                    borderTopWidth:    { type: 'number' },
                    borderTopColor:    { type: 'string' },
                    borderTopStyle:    { type: 'string', enum: ['solid','dashed','dotted','none'] },
                    borderRightWidth:  { type: 'number' },
                    borderRightColor:  { type: 'string' },
                    borderRightStyle:  { type: 'string', enum: ['solid','dashed','dotted','none'] },
                    borderBottomWidth: { type: 'number' },
                    borderBottomColor: { type: 'string' },
                    borderBottomStyle: { type: 'string', enum: ['solid','dashed','dotted','none'] },
                    borderLeftWidth:   { type: 'number' },
                    borderLeftColor:   { type: 'string' },
                    borderLeftStyle:   { type: 'string', enum: ['solid','dashed','dotted','none'] },
                    borderRadius:    { type: 'number' },
                    rotation:        { type: 'number', description: 'Degrees' },
                    strokeWidth:     { type: 'number' },
                    opacity:         { type: 'number', minimum: 0, maximum: 1 },
                  },
                },
                hidden:   { type: 'boolean' },
                locked:   { type: 'boolean' },
                visibleExpression: { type: 'string' },
                pageScope: { type: 'string', enum: ['current','all','first','last','odd','even','range'] },
              },
            },
          },
        },
      },
    ],
  });
}
