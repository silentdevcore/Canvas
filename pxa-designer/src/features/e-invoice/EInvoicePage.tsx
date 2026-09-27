/**
 * The Electronic Invoice workspace.
 *
 * It edits one canonical invoice, projects it onto the Designer canvas through a maintained
 * layout, and hands exactly that object to the backend for the XML and the PDF. Nothing on this
 * page recomputes a total, and nothing turns a clean structural result into a conformance claim.
 *
 * Persistence is the Designer's existing pipeline: the invoice lives on the template, so
 * `useDesignerTemplateAutosave` gives it the two-second debounce, the revision-conflict handling,
 * the offline state and the unsaved-changes warning without a second code path.
 */

import React, { useCallback, useEffect, useId, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  FiCheckCircle,
  FiDownload,
  FiExternalLink,
  FiFileText,
  FiLayout,
  FiPlay,
} from 'react-icons/fi';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import { designerPermissions, hasDesignerPermission } from '@/auth/designerPermissions';
import { useProductExperience } from '@/product/ProductExperienceProvider';
import { useDesignerTemplateAutosave } from '@/hooks/useDesignerTemplateAutosave';
import { useEditorStore } from '@/store';
import InvoiceDiagnosticsPanel, { fieldInputId } from './InvoiceDiagnosticsPanel';
import InvoiceLinesTable from './InvoiceLinesTable';
import InvoiceTotalsPanel from './InvoiceTotalsPanel';
import InvoiceValidationStates from './InvoiceValidationStates';
import {
  EINVOICE_PROFILES,
  EINVOICE_VERSIONS,
  type EInvoiceStandard,
} from './invoiceModel';
import { templatesForStandard } from './invoiceTemplates';
import { useEInvoiceActions } from './useEInvoiceActions';
import { useEInvoiceDocument } from './useEInvoiceDocument';

const STANDARDS: readonly EInvoiceStandard[] = ['FacturX', 'ZUGFeRD', 'XRechnung'];

/** A labelled text input. Every field has a real label; none relies on a placeholder. */
interface FieldProps {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'date' | 'number';
  invalid?: boolean;
  min?: number;
  max?: number;
  describedBy?: string;
}

const Field: React.FC<FieldProps> = ({
  id, label, value, onChange, type = 'text', invalid, min, max, describedBy,
}) => (
  <div className="einv-field">
    <label htmlFor={id}>{label}</label>
    <input
      id={id}
      className="einv-input"
      type={type}
      value={value}
      min={min}
      max={max}
      aria-invalid={invalid || undefined}
      aria-describedby={describedBy}
      onChange={event => onChange(event.target.value)}
    />
  </div>
);

const EInvoicePage: React.FC = () => {
  const { t } = useTranslation('eInvoice');
  const navigate = useNavigate();
  const { user } = useDesignerAuth();
  const { features } = useProductExperience();
  const currentTemplate = useEditorStore(state => state.currentTemplate);
  const setCurrentTemplate = useEditorStore(state => state.setCurrentTemplate);
  const [pendingFocus, setPendingFocus] = useState<string | null>(null);
  const liveRegionId = useId();

  const feature = features.find(candidate => candidate.id === 'designer.e-invoice');
  const featureEnabled = feature?.enabled !== false;
  const actions = useEInvoiceActions(navigate);
  const controller = useEInvoiceDocument(actions.report?.conformanceStatus);
  const autosave = useDesignerTemplateAutosave(Boolean(currentTemplate) && featureEnabled);

  const canGenerate = hasDesignerPermission(user, designerPermissions.pdfEInvoicesGenerate);
  const canCreate = hasDesignerPermission(user, designerPermissions.pdfEInvoicesCreate);
  const canValidate = hasDesignerPermission(user, designerPermissions.pdfEInvoicesValidate);

  const { document: invoiceDocument, totals, findings } = controller;
  const invoiceRevision = useMemo(
    () => (invoiceDocument ? JSON.stringify(invoiceDocument) : ''),
    [invoiceDocument],
  );
  const blocking = useMemo(
    () => findings.some(finding => finding.severity === 'error'),
    [findings],
  );

  const fieldError = useCallback(
    (field: string) => findings.some(f => f.severity === 'error' && f.field === field),
    [findings],
  );

  // Focusing happens after the render that shows the field, so a finding for a line that was
  // just re-ordered still lands on the right input.
  useEffect(() => {
    if (!pendingFocus) return;
    const element = window.document.getElementById(fieldInputId(pendingFocus));
    element?.focus();
    if (element && 'scrollIntoView' in element) {
      element.scrollIntoView({ block: 'center', behavior: 'smooth' });
    }
    setPendingFocus(null);
  }, [pendingFocus]);

  // Unlike /pdf/create, this route has no separate page component that opens a blank document.
  // Create one on direct navigation so the workspace cannot remain in a permanent loading state.
  useEffect(() => {
    if (!featureEnabled || currentTemplate) return;
    setCurrentTemplate({
      id: `e-invoice-${Date.now()}`,
      name: t('page.defaultName'),
      category: 'invoice',
      description: '',
      pages: [{ id: 'page-1', elements: [] }],
      sharedElements: [],
      data: {},
    });
  }, [currentTemplate, featureEnabled, setCurrentTemplate, t]);

  // Generated files and validation evidence describe one exact canonical revision. As soon as
  // the user changes that revision, remove them and invalidate any request still in flight.
  useEffect(() => {
    actions.reset();
  }, [actions.reset, invoiceRevision]);

  if (!featureEnabled) {
    return (
      <div className="einv-page" data-testid="einvoice-unavailable">
        <div className="einv-state-card">
          <h1>{t('unavailable.title')}</h1>
          <p>{t('unavailable.description')}</p>
        </div>
      </div>
    );
  }

  if (!currentTemplate) {
    return (
      <div className="einv-page" data-testid="einvoice-loading">
        <div className="einv-state-card">
          <p role="status">{t('loading')}</p>
        </div>
      </div>
    );
  }

  if (!invoiceDocument) {
    return (
      <div className="einv-page" data-testid="einvoice-empty">
        <div className="einv-state-card">
          <h1>{t('empty.title')}</h1>
          <p>{t('empty.description')}</p>
          <button
            type="button"
            className="einv-btn einv-btn-primary"
            onClick={controller.start}
          >
            <FiFileText aria-hidden="true" />
            {t('empty.action')}
          </button>
          <p className="einv-hint">{t('boundaries.description')}</p>
        </div>
      </div>
    );
  }

  const { invoice, settings } = invoiceDocument;
  const profiles = EINVOICE_PROFILES[settings.standard];
  const layouts = templatesForStandard(settings.standard);

  return (
    <div className="einv-page" data-testid="einvoice-page">
      <header className="einv-header">
        <div>
          <h1>{t('page.title')}</h1>
          <p className="einv-subtitle">{t('page.subtitle')}</p>
        </div>
        <div className="einv-save" data-testid="einvoice-save-state" data-state={autosave.state}>
          <span className="einv-save-label">{t('save.label')}</span>
          <span className={`einv-save-value is-${autosave.state}`} role="status">
            {t(`save.${autosave.state}`)}
          </span>
          {autosave.state === 'conflict' && (
            <span className="einv-save-actions">
              <button type="button" className="einv-link-btn" onClick={() => void autosave.reloadServer()}>
                {t('save.reload')}
              </button>
              <button type="button" className="einv-link-btn" onClick={() => void autosave.saveAsNew()}>
                {t('save.saveAsNew')}
              </button>
            </span>
          )}
          {(autosave.state === 'offline' || autosave.state === 'failed') && (
            <button type="button" className="einv-link-btn" onClick={autosave.downloadLocal}>
              {t('save.downloadLocal')}
            </button>
          )}
        </div>
      </header>

      {/* One live region for every asynchronous result, so a validation or generation outcome is
          announced rather than only appearing. */}
      <p id={liveRegionId} className="einv-visually-hidden" role="status" aria-live="polite">
        {actions.announcement ? t(actions.announcement.key) : ''}
      </p>

      <div className="einv-workspace">
        <div className="einv-column">
          <section className="einv-panel" aria-labelledby="einv-standards-heading">
            <h2 id="einv-standards-heading">{t('sections.standards')}</h2>
            <div className="einv-grid">
              <div className="einv-field">
                <label htmlFor="einv-standard">{t('standard')}</label>
                <select
                  id="einv-standard"
                  className="einv-select"
                  value={settings.standard}
                  onChange={event => controller.selectStandard(event.target.value as EInvoiceStandard)}
                >
                  {STANDARDS.map(standard => (
                    <option key={standard} value={standard}>{standard}</option>
                  ))}
                </select>
              </div>
              <div className="einv-field">
                <label htmlFor="einv-version">{t('version')}</label>
                {/* The maintained version is not a choice: PXA supports exactly one per standard. */}
                <output id="einv-version" className="einv-derived" data-testid="einvoice-version">
                  {EINVOICE_VERSIONS[settings.standard]}
                </output>
              </div>
              <div className="einv-field">
                <label htmlFor="einv-profile">{t('profile')}</label>
                <select
                  id="einv-profile"
                  className="einv-select"
                  value={settings.profile}
                  onChange={event => controller.selectProfile(event.target.value)}
                >
                  {profiles.map(profile => (
                    <option key={profile} value={profile}>{profile}</option>
                  ))}
                </select>
              </div>
            </div>
          </section>

          <section className="einv-panel" aria-labelledby="einv-document-heading">
            <h2 id="einv-document-heading">{t('sections.document')}</h2>
            <div className="einv-grid">
              <Field
                id="einv-invoiceNumber"
                label={t('fields.invoiceNumber')}
                value={invoice.invoiceNumber}
                invalid={fieldError('invoiceNumber')}
                onChange={value => controller.updateInvoice(current => ({ ...current, invoiceNumber: value }))}
              />
              <Field
                id="einv-issueDate"
                type="date"
                label={t('fields.issueDate')}
                value={invoice.issueDate}
                invalid={fieldError('issueDate')}
                onChange={value => controller.updateInvoice(current => ({ ...current, issueDate: value }))}
              />
              <Field
                id="einv-dueDate"
                type="date"
                label={t('fields.dueDate')}
                value={invoice.dueDate ?? ''}
                invalid={fieldError('dueDate')}
                onChange={value => controller.updateInvoice(current => ({ ...current, dueDate: value || undefined }))}
              />
              <Field
                id="einv-currency"
                label={t('fields.currency')}
                value={invoice.currency}
                invalid={fieldError('currency')}
                onChange={value => controller.updateInvoice(current => ({ ...current, currency: value.toUpperCase() }))}
              />
              <Field
                id="einv-currencyDecimalPlaces"
                type="number"
                min={0}
                max={4}
                label={t('fields.currencyDecimalPlaces')}
                value={String(invoice.currencyDecimalPlaces)}
                invalid={fieldError('currencyDecimalPlaces')}
                onChange={value => controller.updateInvoice(current => ({
                  ...current,
                  currencyDecimalPlaces: Number(value),
                }))}
              />
              <Field
                id="einv-buyerReference"
                label={t('fields.buyerReference')}
                value={invoice.buyerReference ?? ''}
                invalid={fieldError('buyerReference')}
                onChange={value => controller.updateInvoice(current => ({ ...current, buyerReference: value }))}
              />
              <Field
                id="einv-paymentReference"
                label={t('fields.paymentReference')}
                value={invoice.paymentReference ?? ''}
                onChange={value => controller.updateInvoice(current => ({ ...current, paymentReference: value }))}
              />
            </div>
          </section>

          {(['seller', 'buyer'] as const).map(role => {
            const party = invoice[role];
            return (
              <section className="einv-panel" key={role} aria-labelledby={`einv-${role}-heading`}>
                <h2 id={`einv-${role}-heading`}>{t(`sections.${role}`)}</h2>
                <div className="einv-grid">
                  <Field
                    id={`einv-${role}-name`}
                    label={t('fields.name')}
                    value={party.name}
                    invalid={fieldError(`${role}.name`)}
                    onChange={value => controller.updateInvoice(current => ({
                      ...current,
                      [role]: { ...current[role], name: value },
                    }))}
                  />
                  <Field
                    id={`einv-${role}-taxIdentifier`}
                    label={t('fields.taxIdentifier')}
                    value={party.taxIdentifier ?? ''}
                    onChange={value => controller.updateInvoice(current => ({
                      ...current,
                      [role]: { ...current[role], taxIdentifier: value },
                    }))}
                  />
                  <Field
                    id={`einv-${role}-address-street`}
                    label={t('fields.street')}
                    value={party.address?.street ?? ''}
                    invalid={fieldError(`${role}.address.street`)}
                    onChange={value => controller.updateInvoice(current => ({
                      ...current,
                      [role]: {
                        ...current[role],
                        address: { ...emptyAddress, ...current[role].address, street: value },
                      },
                    }))}
                  />
                  <Field
                    id={`einv-${role}-address-postalCode`}
                    label={t('fields.postalCode')}
                    value={party.address?.postalCode ?? ''}
                    invalid={fieldError(`${role}.address.postalCode`)}
                    onChange={value => controller.updateInvoice(current => ({
                      ...current,
                      [role]: {
                        ...current[role],
                        address: { ...emptyAddress, ...current[role].address, postalCode: value },
                      },
                    }))}
                  />
                  <Field
                    id={`einv-${role}-address-city`}
                    label={t('fields.city')}
                    value={party.address?.city ?? ''}
                    invalid={fieldError(`${role}.address.city`)}
                    onChange={value => controller.updateInvoice(current => ({
                      ...current,
                      [role]: {
                        ...current[role],
                        address: { ...emptyAddress, ...current[role].address, city: value },
                      },
                    }))}
                  />
                  <Field
                    id={`einv-${role}-address-countryCode`}
                    label={t('fields.countryCode')}
                    value={party.address?.countryCode ?? ''}
                    invalid={fieldError(`${role}.address.countryCode`)}
                    onChange={value => controller.updateInvoice(current => ({
                      ...current,
                      [role]: {
                        ...current[role],
                        address: {
                          ...emptyAddress,
                          ...current[role].address,
                          countryCode: value.toUpperCase(),
                        },
                      },
                    }))}
                  />
                </div>
              </section>
            );
          })}

          <section className="einv-panel" aria-labelledby="einv-lines-heading">
            <h2 id="einv-lines-heading">{t('sections.lines')}</h2>
            {totals && (
              <InvoiceLinesTable
                lines={invoice.lines}
                totals={totals}
                findings={findings}
                onAdd={controller.addLine}
                onRemove={controller.removeLine}
                onMove={controller.moveLine}
                onChange={controller.updateLine}
              />
            )}
          </section>
        </div>

        <aside className="einv-column is-side">
          <section className="einv-panel" aria-labelledby="einv-totals-heading">
            <h2 id="einv-totals-heading">{t('sections.totals')}</h2>
            {totals && <InvoiceTotalsPanel totals={totals} currency={invoice.currency} />}
          </section>

          <section className="einv-panel" aria-labelledby="einv-layout-heading">
            <h2 id="einv-layout-heading">{t('sections.templates')}</h2>
            <p className="einv-hint">{t('templates.description')}</p>
            <ul className="einv-template-list">
              {layouts.map(layout => (
                <li key={layout.id}>
                  <div>
                    <span className="einv-template-name">{t(layout.nameKey)}</span>
                    <span className="einv-template-blurb">{t(layout.descriptionKey)}</span>
                  </div>
                  <button
                    type="button"
                    className="einv-btn einv-btn-secondary"
                    data-testid={`einvoice-insert-${layout.id}`}
                    onClick={() => controller.insertTemplate(layout.id)}
                  >
                    <FiLayout aria-hidden="true" />
                    {invoiceDocument.templateId === layout.id
                      ? t('templates.active')
                      : t('templates.insert')}
                  </button>
                </li>
              ))}
            </ul>
            {!controller.hasTemplate && <p className="einv-hint">{t('templates.none')}</p>}
          </section>

          <section className="einv-panel" aria-labelledby="einv-validation-heading">
            <h2 id="einv-validation-heading">{t('sections.diagnostics')}</h2>
            <InvoiceValidationStates
              findings={findings}
              conformance={controller.conformance}
              report={actions.report}
            />
            <InvoiceDiagnosticsPanel
              findings={findings}
              report={actions.report}
              onFocusField={setPendingFocus}
            />
          </section>

          <section className="einv-panel" aria-labelledby="einv-actions-heading">
            <h2 id="einv-actions-heading">{t('sections.actions')}</h2>
            <div className="einv-actions">
              <button
                type="button"
                className="einv-btn einv-btn-secondary"
                data-testid="einvoice-validate"
                disabled={!canValidate || blocking || actions.busy !== null}
                onClick={() => void actions.validate(invoice, settings)}
              >
                <FiCheckCircle aria-hidden="true" />
                {t('actions.validate')}
              </button>
              <button
                type="button"
                className="einv-btn einv-btn-secondary"
                data-testid="einvoice-generate-xml"
                disabled={!canGenerate || blocking || actions.busy !== null}
                onClick={() => void actions.generateXml(invoice, settings)}
              >
                <FiPlay aria-hidden="true" />
                {t('actions.generateXml')}
              </button>
              <button
                type="button"
                className="einv-btn einv-btn-primary"
                data-testid="einvoice-create-pdf"
                disabled={!canCreate || blocking || actions.busy !== null}
                onClick={() => void actions.createPdf(invoice, settings)}
              >
                <FiFileText aria-hidden="true" />
                {t('actions.createPdf')}
              </button>
            </div>

            {actions.error && (
              <p className="einv-error" role="alert" data-testid="einvoice-error">
                {t(actions.error.key)}
                {actions.error.code && (
                  <code className="einv-diagnostic-code">
                    {`${t('errors.serverCode')}: ${actions.error.code}`}
                  </code>
                )}
              </p>
            )}

            <div className="einv-results" data-testid="einvoice-results">
              {!actions.artifacts.xml && !actions.artifacts.pdf && (
                <p className="einv-hint">{t('result.none')}</p>
              )}
              {actions.artifacts.xml && (
                <div className="einv-result-row">
                  <span>{t('result.xmlReady')}</span>
                  <button
                    type="button"
                    className="einv-link-btn"
                    data-testid="einvoice-download-xml"
                    onClick={actions.downloadXml}
                  >
                    <FiDownload aria-hidden="true" />
                    {t('actions.downloadXml')}
                  </button>
                </div>
              )}
              {actions.artifacts.pdf && (
                <div className="einv-result-row">
                  <span>{t('result.pdfReady')}</span>
                  <button
                    type="button"
                    className="einv-link-btn"
                    data-testid="einvoice-download-pdf"
                    onClick={actions.downloadPdf}
                  >
                    <FiDownload aria-hidden="true" />
                    {t('actions.downloadPdf')}
                  </button>
                  <button
                    type="button"
                    className="einv-link-btn"
                    data-testid="einvoice-open-viewer"
                    onClick={actions.openInViewer}
                  >
                    <FiExternalLink aria-hidden="true" />
                    {t('actions.openInViewer')}
                  </button>
                </div>
              )}
              {actions.inspection && (
                <div
                  className="einv-consistency"
                  data-testid="einvoice-consistency"
                  data-status={actions.inspection.pdfConsistency.status}
                >
                  <span className="einv-state-label">{t('consistency.label')}</span>
                  <span>{t(`consistency.${actions.inspection.pdfConsistency.status}`)}</span>
                </div>
              )}
            </div>
          </section>

          <section className="einv-panel is-muted" aria-labelledby="einv-boundaries-heading">
            <h2 id="einv-boundaries-heading">{t('boundaries.title')}</h2>
            <p className="einv-hint">{t('boundaries.description')}</p>
          </section>
        </aside>
      </div>
    </div>
  );
};

const emptyAddress = { street: '', city: '', postalCode: '', countryCode: '' };

export default EInvoicePage;
