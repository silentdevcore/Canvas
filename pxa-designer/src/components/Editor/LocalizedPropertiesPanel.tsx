import React, { useCallback, useRef, useState } from 'react';
import { FiGlobe, FiTrash2, FiPlus } from 'react-icons/fi';
import { useTranslation } from 'react-i18next';
import { useEditorStore } from '@/store';
import type { LocalizedProperty } from '@/types';

const RTL_LANGS = new Set(['ar', 'he', 'fa', 'ur', 'yi', 'dv']);

export const LocalizedPropertiesPanel: React.FC = () => {
  const { t } = useTranslation('editor');
  const {
    pageSettings,
    currentPreviewLanguage,
    upsertLocalizedProperty,
    deleteLocalizedProperty,
  } = useEditorStore();

  // Use refs for the add form so values are never stale in callbacks
  const newKeyRef = useRef('');
  const newValueRef = useRef('');
  const [newKeyDisplay, setNewKeyDisplay] = useState('');
  const [newValueDisplay, setNewValueDisplay] = useState('');
  const [newScope, setNewScope] = useState<'global' | 'own'>('global');

  const props = pageSettings.localizedProperties ?? [];
  const langs = pageSettings.activeLanguages ?? [];
  const sysLang = navigator.language.split('-')[0];
  const activeLang = currentPreviewLanguage || sysLang;
  const isRtl = RTL_LANGS.has(activeLang);

  const visibleProps = props.filter(
    p => p.scope === 'global' || p.ownerLanguage === activeLang
  );

  const addProperty = useCallback(() => {
    const key = newKeyRef.current.replace(/\{\{|\}\}/g, '').trim().toUpperCase();
    if (!key) return;
    const value = newValueRef.current;
    const prop: LocalizedProperty = newScope === 'own'
      ? { key, scope: 'own', ownerLanguage: activeLang, localizedValues: { [activeLang]: value } }
      : { key, scope: 'global', localizedValues: { [activeLang]: value } };
    upsertLocalizedProperty(prop);
    newKeyRef.current = '';
    newValueRef.current = '';
    setNewKeyDisplay('');
    setNewValueDisplay('');
  }, [newScope, activeLang, upsertLocalizedProperty]);

  const setLocalizedValue = (prop: LocalizedProperty, lang: string, value: string) => {
    upsertLocalizedProperty({
      ...prop,
      localizedValues: { ...prop.localizedValues, [lang]: value },
    });
  };

  const setScope = (prop: LocalizedProperty, scope: 'global' | 'own') => {
    if (scope === 'own') {
      upsertLocalizedProperty({ ...prop, scope: 'own', ownerLanguage: activeLang });
    } else {
      const { ownerLanguage: _o, ...rest } = prop;
      upsertLocalizedProperty({ ...rest, scope: 'global' });
    }
  };

  return (
    <div className="editor-settings-section">
      <div className="editor-settings-heading">
        <FiGlobe />
        <span>{t('localizedProperties.heading')}</span>
      </div>
      <div className="editor-form-stack" style={{ padding: 12 }}>

        {langs.length > 1 && (
          <div style={{ fontSize: 11, color: '#64748b', marginBottom: 6 }}>
            {t('localizedProperties.editing')} <strong>{activeLang.toUpperCase()}</strong>
            {isRtl && <span style={{ marginLeft: 4, color: '#f59e0b' }}>RTL</span>}
          </div>
        )}

        {visibleProps.length === 0 && (
          <div style={{ fontSize: 12, color: '#94a3b8', padding: '4px 0' }}>
            {t('localizedProperties.empty')}
          </div>
        )}

        {visibleProps.map((prop) => {
          const isOwn = prop.scope === 'own';
          const missingLangs = isOwn ? [] : langs.filter(l => !prop.localizedValues[l]);

          return (
            <div key={prop.key} style={{ border: '1px solid var(--editor-border, #e2e8f0)', borderRadius: 6, padding: 8, marginBottom: 6 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
                <code style={{ fontSize: 12, fontFamily: 'monospace', flex: 1, color: '#6366f1' }}>
                  {`{{${prop.key}}}`}
                </code>

                {isOwn ? (
                  <span
                    title={t('localizedProperties.ownTitle')}
                    style={{ fontSize: 10, padding: '2px 6px', borderRadius: 4, background: '#fef3c7', color: '#92400e' }}
                  >
                    {t('localizedProperties.own')} · {activeLang.toUpperCase()}
                  </span>
                ) : (
                  <span
                    title={t('localizedProperties.globalTitle')}
                    style={{ fontSize: 10, padding: '2px 6px', borderRadius: 4, background: '#ede9fe', color: '#4c1d95' }}
                  >
                    {t('localizedProperties.global')}
                  </span>
                )}

                <div style={{ display: 'flex', border: '1px solid var(--editor-border, #e2e8f0)', borderRadius: 4, overflow: 'hidden', fontSize: 10 }}>
                  {(['global', 'own'] as const).map((mode) => {
                    const active = prop.scope === mode;
                    return (
                      <button
                        key={mode}
                        onClick={() => setScope(prop, mode)}
                        title={mode === 'global'
                          ? t('localizedProperties.globalModeTitle')
                          : t('localizedProperties.ownModeTitle', { language: activeLang.toUpperCase() })}
                        style={{
                          padding: '2px 6px', border: 'none',
                          background: active ? 'var(--editor-accent, #6366f1)' : 'white',
                          color: active ? 'white' : '#374151',
                          cursor: 'pointer', fontSize: 10,
                        }}
                      >
                        {mode === 'global' ? t('localizedProperties.global') : t('localizedProperties.own')}
                      </button>
                    );
                  })}
                </div>

                <button
                  className="editor-icon-button"
                  title={t('localizedProperties.delete')}
                  onClick={() => deleteLocalizedProperty(prop.key)}
                >
                  <FiTrash2 size={12} />
                </button>
              </div>

              {isOwn ? (
                <div>
                  <input
                    type="text"
                    placeholder={t('localizedProperties.valueOwn', { language: activeLang.toUpperCase() })}
                    value={prop.localizedValues[activeLang] ?? ''}
                    dir={isRtl ? 'rtl' : 'ltr'}
                    onChange={(e) => setLocalizedValue(prop, activeLang, e.target.value)}
                    style={{ width: '100%', boxSizing: 'border-box' }}
                  />
                  <div style={{ fontSize: 10, color: '#92400e', marginTop: 3 }}>
                    {t('localizedProperties.notExported')}
                  </div>
                </div>
              ) : (
                <div>
                  <input
                    type="text"
                    placeholder={t('localizedProperties.value', { language: activeLang.toUpperCase() })}
                    value={prop.localizedValues[activeLang] ?? ''}
                    dir={isRtl ? 'rtl' : 'ltr'}
                    onChange={(e) => setLocalizedValue(prop, activeLang, e.target.value)}
                    style={{ width: '100%', boxSizing: 'border-box', marginBottom: 4 }}
                  />
                  <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                    {langs.map(lang => {
                      const hasValue = !!prop.localizedValues[lang];
                      return (
                        <span
                          key={lang}
                          title={hasValue
                            ? `${lang}: "${prop.localizedValues[lang]}"`
                            : t('localizedProperties.languageMissing', { language: lang })}
                          style={{
                            fontSize: 10, padding: '1px 5px', borderRadius: 3,
                            background: hasValue ? '#d1fae5' : '#fee2e2',
                            color: hasValue ? '#065f46' : '#991b1b',
                            fontFamily: 'monospace',
                          }}
                        >
                          {lang}
                        </span>
                      );
                    })}
                  </div>
                  {missingLangs.length > 0 && (
                    <div style={{ fontSize: 10, color: '#ef4444', marginTop: 3 }}>
                      {t('localizedProperties.missing', { languages: missingLangs.join(', ') })}
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}

        {/* Add new property */}
        <div style={{ marginTop: 8, borderTop: '1px solid var(--editor-border, #e2e8f0)', paddingTop: 8 }}>
          <div style={{ fontSize: 11, color: '#64748b', marginBottom: 6 }}>
            {t('localizedProperties.add')}
          </div>

          <div style={{ display: 'flex', gap: 4, marginBottom: 6 }}>
            {(['global', 'own'] as const).map((mode) => (
              <button
                key={mode}
                onClick={() => setNewScope(mode)}
                style={{
                  padding: '3px 8px', borderRadius: 4, fontSize: 11, cursor: 'pointer',
                  border: '1px solid var(--editor-border, #e2e8f0)',
                  background: newScope === mode ? 'var(--editor-accent, #6366f1)' : 'white',
                  color: newScope === mode ? 'white' : '#374151',
                  flex: 1,
                }}
              >
                {mode === 'global'
                  ? t('localizedProperties.globalAll')
                  : t('localizedProperties.ownOnly', { language: activeLang.toUpperCase() })}
              </button>
            ))}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr auto', gap: 4 }}>
            <input
              type="text"
              placeholder={t('localizedProperties.keyPlaceholder')}
              value={newKeyDisplay}
              onChange={(e) => {
                newKeyRef.current = e.target.value;
                setNewKeyDisplay(e.target.value);
              }}
              onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addProperty(); } }}
            />
            <input
              type="text"
              placeholder={t('localizedProperties.valuePlaceholder', { language: activeLang.toUpperCase() })}
              value={newValueDisplay}
              dir={isRtl ? 'rtl' : 'ltr'}
              onChange={(e) => {
                newValueRef.current = e.target.value;
                setNewValueDisplay(e.target.value);
              }}
              onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addProperty(); } }}
            />
            <button className="editor-primary-button" onClick={addProperty} title={t('localizedProperties.add')}>
              <FiPlus size={13} />
            </button>
          </div>
          <div style={{ fontSize: 10, color: '#94a3b8', marginTop: 4 }}>
            {t('localizedProperties.usageBefore')} <code>{'{{KEY}}'}</code>{' '}
            {t('localizedProperties.usageAfter')}
          </div>
        </div>
      </div>
    </div>
  );
};

export default LocalizedPropertiesPanel;
