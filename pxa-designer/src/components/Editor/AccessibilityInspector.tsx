import React, { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { FiCheckCircle, FiEyeOff } from 'react-icons/fi';
import type { SimpleElement } from '@/types';
import {
  PDF_STRUCTURE_ROLES,
  accessibilityIssuesForElement,
  suggestedAccessibility,
  type DesignerAccessibilityIssue,
} from '@/accessibility/designerAccessibility';

interface Props {
  element: SimpleElement;
  allElements: SimpleElement[];
  issues: DesignerAccessibilityIssue[];
  onChange: (updates: Partial<SimpleElement>) => void;
}

const AccessibilityInspector: React.FC<Props> = ({ element, allElements, issues, onChange }) => {
  const { t } = useTranslation('editor');
  const value = element.accessibility;
  const elementIssues = useMemo(() => accessibilityIssuesForElement(element.id, issues), [element.id, issues]);
  const update = (patch: Partial<NonNullable<SimpleElement['accessibility']>>) => {
    onChange({ accessibility: { ...(value ?? suggestedAccessibility(element)), ...patch } });
  };

  return (
    <section className="editor-settings-section" data-testid="accessibility-inspector">
      <div className="editor-settings-heading">
        <FiCheckCircle />
        <span>{t('accessibility.heading')}</span>
      </div>
      <div className="editor-form-stack editor-accessibility-fields">
        {!value && (
          <button className="editor-secondary-button" type="button" onClick={() => onChange({ accessibility: suggestedAccessibility(element) })}>
            {t('accessibility.useSuggested')}
          </button>
        )}
        <label className="editor-checkbox-control">
          <input
            type="checkbox"
            checked={value?.isArtifact ?? false}
            onChange={event => update({ isArtifact: event.target.checked })}
          />
          <FiEyeOff />
          <span>{t('accessibility.decorative')}</span>
        </label>
        {!(value?.isArtifact ?? false) && (
          <>
            <label>
              <span>{t('accessibility.role')}</span>
              <select value={value?.role ?? ''} onChange={event => update({ role: event.target.value as any })}>
                <option value="" disabled>{t('accessibility.selectRole')}</option>
                {PDF_STRUCTURE_ROLES.map(role => <option key={role} value={role}>{role}</option>)}
              </select>
            </label>
            <label>
              <span>{t('accessibility.customRole')}</span>
              <input type="text" value={value?.customRole ?? ''} placeholder={t('accessibility.customRolePlaceholder')}
                onChange={event => update({ customRole: event.target.value || undefined })} />
            </label>
            <label>
              <span>{t('accessibility.namespaceUri')}</span>
              <input type="url" value={value?.namespaceUri ?? ''} placeholder={t('accessibility.namespaceUriPlaceholder')}
                disabled={!value?.customRole}
                onChange={event => update({ namespaceUri: event.target.value || undefined })} />
            </label>
            <label>
              <span>{t('accessibility.readingOrder')}</span>
              <input type="number" min={0} step={1} value={value?.readingOrder ?? ''}
                onChange={event => update({ readingOrder: event.target.value === '' ? undefined : Number(event.target.value) })} />
            </label>
            <label>
              <span>{t('accessibility.language')}</span>
              <input type="text" value={value?.language ?? ''} placeholder={t('accessibility.languagePlaceholder')}
                onChange={event => update({ language: event.target.value || undefined })} />
            </label>
            <label>
              <span>{t('accessibility.parent')}</span>
              <select value={value?.parentElementId ?? ''} onChange={event => update({ parentElementId: event.target.value || undefined })}>
                <option value="">{t('accessibility.noParent')}</option>
                {allElements.filter(candidate => candidate.id !== element.id && candidate.accessibility && !candidate.accessibility.isArtifact).map(candidate => (
                  <option key={candidate.id} value={candidate.id}>{candidate.name || candidate.type}</option>
                ))}
              </select>
            </label>
            <label>
              <span>{t('accessibility.alternateText')}</span>
              <textarea rows={3} value={value?.alternateText ?? ''}
                onChange={event => update({ alternateText: event.target.value || undefined })} />
            </label>
            <label>
              <span>{t('accessibility.actualText')}</span>
              <textarea rows={2} value={value?.actualText ?? ''}
                onChange={event => update({ actualText: event.target.value || undefined })} />
            </label>
            {value?.role === 'TH' && (
              <label>
                <span>{t('accessibility.headerScope')}</span>
                <select value={value.tableHeaderScope ?? 'None'} onChange={event => update({ tableHeaderScope: event.target.value as any })}>
                  {(['None', 'Row', 'Column', 'Both'] as const).map(scope => (
                    <option key={scope} value={scope}>{t(`accessibility.scopes.${scope}`)}</option>
                  ))}
                </select>
              </label>
            )}
            {value?.role === 'TD' && (
              <label>
                <span>{t('accessibility.headerIds')}</span>
                <input type="text" value={(value.tableHeaderIds ?? []).join(', ')} placeholder={t('accessibility.headerIdsPlaceholder')}
                  onChange={event => update({ tableHeaderIds: event.target.value.split(',').map(item => item.trim()).filter(Boolean) })} />
              </label>
            )}
          </>
        )}
        {elementIssues.length > 0 ? (
          <div className="editor-accessibility-issues" role="alert">
            {elementIssues.map(issue => <p key={issue.code}>{t(`accessibility.diagnostics.${issue.code}`)}</p>)}
          </div>
        ) : value ? (
          <small className="editor-accessibility-valid">{t('accessibility.elementReady')}</small>
        ) : null}
      </div>
    </section>
  );
};

export default AccessibilityInspector;
