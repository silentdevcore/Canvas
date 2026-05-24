import { useCallback, useEffect, useRef, useState } from 'react';
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

export default function JsonEditorPane({ value, language, onChange, onCsharpConvert }: Props) {
  const editorRef = useRef<any>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [copied, setCopied] = useState(false);
  const [isRunning, setIsRunning] = useState(false);

  const handleMount: OnMount = (editor, monaco) => {
    editorRef.current = editor;
    if (language === 'json') configureSchema(monaco); // JSON schema only in JSON mode

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
          {language === 'csharp-code' ? 'C# Code' : language === 'csharp-dto' ? 'C# DTO' : 'JSON'}
        </span>

        <div className="code-editor-toolbar-group">
          {language === 'json' && <span className="code-editor-hint">⌘↵ refresh</span>}
          {language !== 'json' && <span className="code-editor-hint">⌘↵ run</span>}
          <button className="code-editor-btn" onClick={handleFormat} title="Format">
            ⌥ Format
          </button>
          <button className="code-editor-btn" onClick={handleCopy} title="Copy">
            {copied ? '✓ Copied' : 'Copy'}
          </button>
          {language !== 'json' && (
            <button
              className={`code-editor-run-btn${isRunning ? ' is-running' : ''}`}
              onClick={handleRun}
              disabled={isRunning}
              title="Execute C# and update preview"
            >
              {isRunning ? '⏳ Running…' : '▶ Run'}
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
                title={`Load ${STARTER_LABELS[key]} template`}
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
    </div>
  );
}

function configureSchema(monaco: Monaco) {
  monaco.languages.json.jsonDefaults.setDiagnosticsOptions({
    validate: true,
    schemas: [
      {
        uri: 'canvas://design-schema',
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
                    borderRadius:    { type: 'number' },
                    rotation:        { type: 'number', description: 'Degrees' },
                    strokeWidth:     { type: 'number' },
                    opacity:         { type: 'number', minimum: 0, maximum: 1 },
                  },
                },
                hidden:   { type: 'boolean' },
                locked:   { type: 'boolean' },
                pageScope: { type: 'string', enum: ['current','all','first','odd','even','range'] },
              },
            },
          },
        },
      },
    ],
  });
}
