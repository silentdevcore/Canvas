import React, { useMemo, useState } from 'react';
import Editor from '@monaco-editor/react';
import { FiCode, FiCopy, FiDownload, FiExternalLink, FiRefreshCw } from 'react-icons/fi';
import { useTranslation } from 'react-i18next';
import MigrationTabs, { pdfTabs } from '@/components/Migrations/MigrationTabs';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import { migrationProviderPermission } from '@/auth/designerPermissions';
import { openMigratedPdfViewer, type MigratedPdfViewerConfig } from '@/features/pdf-viewer/migrationAdapter';
import { transpileNutrientViewerTypeScript } from '@/features/pdf-viewer/nutrientTypeScript';

type InputType = 'javascript' | 'typescript' | 'instantJson';
type OutputType = 'module' | 'settings' | 'annotations' | 'forms';

interface MigrationDiagnostic {
  code: string;
  severity: string;
  message: string;
}

interface ViewerMigrationResult {
  detectedProduct?: string;
  sourceLanguage?: string;
  documentFidelity: string;
  sourcePreservation: string;
  normalizedViewerSettingsJson?: string;
  generatedIntegrationModule?: string;
  annotationSidecarJson?: string;
  formValuePayloadJson?: string;
  diagnostics: MigrationDiagnostic[];
  sourceMap?: Array<{ sourceLine: number; sourceColumn: number; targetSymbol: string; targetRoute: string }>;
  sourceChecksum?: string;
  normalizedChecksum?: string;
  generatedChecksum?: string;
  capabilityReport?: {
    migrated: number;
    approximated: number;
    omitted: number;
    reviewRequired: number;
    unsupported: number;
  };
}

interface ComprehensiveExample {
  typeScript: string;
  instantJson: string;
  gdPictureCSharp: string;
  pages: Array<{ width: number; height: number; rotation?: number; cropLeft?: number; cropTop?: number }>;
}

const JAVASCRIPT_EXAMPLE = `import NutrientViewer from "@nutrient-sdk/viewer";

const config = {
  container: "#viewer",
  document: "/documents/sample.pdf",
  initialViewState: { currentPageIndex: 0, zoom: "FIT_TO_WIDTH" },
  toolbarItems: ["pager", "zoom-out", "zoom-in", "print"]
};

NutrientViewer.load(config);`;

const TYPESCRIPT_EXAMPLE = `import NutrientViewer, { type Configuration } from "@nutrient-sdk/viewer";

const config: Configuration = {
  container: "#viewer",
  document: "/documents/sample.pdf",
  initialViewState: { currentPageIndex: 0, zoom: "FIT_TO_WIDTH" }
};

NutrientViewer.load(config);`;

const INSTANT_JSON_EXAMPLE = `{
  "format": "https://pspdfkit.com/instant-json/v1",
  "annotations": []
}`;

const NutrientViewerMigrationPage: React.FC = () => {
  const { t } = useTranslation('migrations');
  const { user } = useDesignerAuth();
  const requiredPermission = migrationProviderPermission('pdf', 'nutrient-viewer');
  const allowed = user.permissions.includes(requiredPermission);
  const [inputType, setInputType] = useState<InputType>('javascript');
  const [source, setSource] = useState(JAVASCRIPT_EXAMPLE);
  const [pageGeometry, setPageGeometry] = useState('[{"width":595,"height":842}]');
  const [result, setResult] = useState<ViewerMigrationResult | null>(null);
  const [outputType, setOutputType] = useState<OutputType>('module');
  const [pending, setPending] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [comprehensiveExample, setComprehensiveExample] = useState<ComprehensiveExample | null>(null);

  const output = useMemo(() => {
    if (!result) return '';
    if (outputType === 'settings') return result.normalizedViewerSettingsJson ?? '';
    if (outputType === 'annotations') return result.annotationSidecarJson ?? '';
    if (outputType === 'forms') return result.formValuePayloadJson ?? '';
    return result.generatedIntegrationModule ?? '';
  }, [outputType, result]);

  const selectInputType = (next: InputType) => {
    setInputType(next);
    setSource(next === 'javascript'
      ? JAVASCRIPT_EXAMPLE
      : next === 'typescript'
        ? comprehensiveExample?.typeScript ?? TYPESCRIPT_EXAMPLE
        : comprehensiveExample?.instantJson ?? INSTANT_JSON_EXAMPLE);
    setResult(null);
    setError(null);
    setOutputType(next === 'instantJson' ? 'annotations' : 'module');
  };

  const loadComprehensiveExample = async () => {
    setPending(true);
    setError(null);
    try {
      const response = await fetch('/api/pxa/v1/migrations/pdf/viewer/examples/comprehensive');
      const body = await response.json();
      if (!response.ok) throw new Error(body.detail ?? body.title ?? `HTTP ${response.status}`);
      const example = body as ComprehensiveExample;
      setComprehensiveExample(example);
      setInputType('typescript');
      setSource(example.typeScript);
      setPageGeometry(JSON.stringify(example.pages, null, 2));
      setOutputType('module');
      setResult(null);
    } catch (exampleError) {
      setError(exampleError instanceof Error ? exampleError.message : t('viewerMigration.exampleFailed'));
    } finally {
      setPending(false);
    }
  };

  const convert = async () => {
    setPending(true);
    setError(null);
    try {
      const pages = inputType === 'instantJson' ? JSON.parse(pageGeometry) : undefined;
      let transpiledSource: string | undefined;
      let compilerDiagnostics: string[] | undefined;
      if (inputType === 'typescript') {
        const transpiled = await transpileNutrientViewerTypeScript(source);
        transpiledSource = transpiled.outputText;
        compilerDiagnostics = transpiled.diagnostics;
      }
      const response = await fetch('/api/pxa/v1/migrations/pdf/viewer/convert', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          inputType,
          source,
          pages,
          transpiledSource,
          compilerDiagnostics,
          sourceName: inputType === 'javascript' ? 'viewer.js' : inputType === 'typescript' ? 'viewer.ts' : 'instant.json',
        }),
      });
      const body = await response.json();
      if (!response.ok) {
        throw new Error(body.detail ?? body.title ?? `HTTP ${response.status}`);
      }
      setResult(body as ViewerMigrationResult);
      setOutputType(inputType === 'instantJson' ? 'annotations' : 'module');
    } catch (conversionError) {
      setResult(null);
      setError(conversionError instanceof Error ? conversionError.message : t('viewerMigration.conversionFailed'));
    } finally {
      setPending(false);
    }
  };

  const copyOutput = async () => {
    if (!output) return;
    await navigator.clipboard.writeText(output);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  };

  const openViewer = () => {
    if (!result?.normalizedViewerSettingsJson) return;
    try {
      openMigratedPdfViewer(JSON.parse(result.normalizedViewerSettingsJson) as MigratedPdfViewerConfig);
    } catch (openError) {
      setError(openError instanceof Error ? openError.message : t('viewerMigration.openFailed'));
    }
  };

  if (!allowed) {
    return (
      <main className="designer-auth-state">
        <section>
          <h1>{t('viewerMigration.permissionTitle')}</h1>
          <p>{t('viewerMigration.permissionMessage')}</p>
          <code>{requiredPermission}</code>
        </section>
      </main>
    );
  }

  return (
    <div className="mgr-page">
      <MigrationTabs tabs={pdfTabs('viewer', t)} />
      <main className="mgr-main">
        <div className="mgr-heading">
          <div className="mgr-heading-left">
            <FiCode className="mgr-heading-icon" />
            <div>
              <h1>{t('viewerMigration.title')}</h1>
              <p>{t('viewerMigration.description')}</p>
            </div>
          </div>
          <button type="button" className="mgr-btn" onClick={loadComprehensiveExample} disabled={pending}>
            <FiDownload /> {t('viewerMigration.loadComprehensiveExample')}
          </button>
        </div>

        <div className="mgr-subtabs" role="tablist" aria-label={t('viewerMigration.inputType')}>
          {(['javascript', 'typescript', 'instantJson'] as const).map(type => (
            <button key={type} type="button" role="tab" aria-selected={inputType === type} className={`mgr-subtab${inputType === type ? ' is-active' : ''}`} onClick={() => selectInputType(type)}>
              {t(`viewerMigration.${type}`)}
            </button>
          ))}
        </div>

        {error && <div className="mgr-error" role="alert">{error}</div>}

        <div className="mgr-split">
          <section className="mgr-pane" style={{ width: '50%' }}>
            <div className="mgr-pane-header"><span>{t('viewerMigration.source')}</span></div>
            <div className="mgr-editor-wrapper">
              <Editor language={inputType === 'instantJson' ? 'json' : inputType} value={source} onChange={value => setSource(value ?? '')} height="100%" options={{ minimap: { enabled: false }, wordWrap: 'on', fontSize: 13 }} />
            </div>
            {inputType === 'instantJson' && (
              <label className="mgr-resource-panel">
                <span>{t('viewerMigration.pageGeometry')}</span>
                <textarea value={pageGeometry} onChange={event => setPageGeometry(event.target.value)} spellCheck={false} />
              </label>
            )}
            <div className="mgr-pane-footer">
              <button type="button" className="mgr-btn mgr-btn-primary" onClick={convert} disabled={pending || !source.trim()}>
                {pending ? <><FiRefreshCw className="mgr-spin" /> {t('converting')}</> : t('convert')}
              </button>
            </div>
          </section>

          <section className="mgr-pane" style={{ flex: 1 }}>
            <div className="mgr-pane-header">
              <div className="mgr-subtabs" role="tablist">
                {(['module', 'settings', 'annotations', 'forms'] as const).map(type => (
                  <button key={type} type="button" role="tab" aria-selected={outputType === type} className={`mgr-subtab${outputType === type ? ' is-active' : ''}`} onClick={() => setOutputType(type)} disabled={type === 'module' ? !result?.generatedIntegrationModule : type === 'settings' ? !result?.normalizedViewerSettingsJson : type === 'annotations' ? !result?.annotationSidecarJson : !result?.formValuePayloadJson}>
                    {t(`viewerMigration.${type}`)}
                  </button>
                ))}
              </div>
              <div className="mgr-pane-header-actions">
                <button type="button" className="mgr-icon-btn" onClick={copyOutput} disabled={!output}><FiCopy /> {copied ? t('copied') : t('copy')}</button>
                <button type="button" className="mgr-icon-btn" onClick={openViewer} disabled={!result?.normalizedViewerSettingsJson}><FiExternalLink /> {t('viewerMigration.openViewer')}</button>
              </div>
            </div>
            <div className="mgr-editor-wrapper">
              <Editor language={outputType === 'module' ? 'typescript' : 'json'} value={output} height="100%" options={{ readOnly: true, minimap: { enabled: false }, wordWrap: 'on', fontSize: 13 }} />
            </div>
          </section>
        </div>

        {result && (
          <section className="mgr-diagnostics">
            <strong>{t('diagnostics.label')}</strong>
            <span>{result.detectedProduct ? `${result.detectedProduct} / ` : ''}{result.sourceLanguage ? `${result.sourceLanguage} / ` : ''}{result.documentFidelity} / {result.sourcePreservation}</span>
            {result.capabilityReport && (
              <p>
                {t('viewerMigration.capabilitySummary', result.capabilityReport)}
              </p>
            )}
            {result.sourceMap?.map(item => (
              <p key={`${item.sourceLine}-${item.targetSymbol}`}>
                <code>{t('viewerMigration.sourceLine', { line: item.sourceLine })}</code> {item.targetSymbol} → {item.targetRoute}
              </p>
            ))}
            {result.diagnostics.map(item => <p key={`${item.code}-${item.message}`}><code>{item.code}</code> {item.message}</p>)}
            {result.sourceChecksum && (
              <details>
                <summary>{t('viewerMigration.checksums')}</summary>
                <p><code>source {result.sourceChecksum}</code></p>
                {result.normalizedChecksum && <p><code>normalized {result.normalizedChecksum}</code></p>}
                {result.generatedChecksum && <p><code>generated {result.generatedChecksum}</code></p>}
              </details>
            )}
          </section>
        )}
      </main>
    </div>
  );
};

export default NutrientViewerMigrationPage;
