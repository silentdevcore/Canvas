import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FiEye, FiFilePlus, FiGrid, FiPlus, FiSearch, FiTrash2, FiX } from 'react-icons/fi';
import {
  CONTENT_CATEGORIES,
  CONTENT_LIBRARY,
  buildContentPages,
  type ContentCategory,
  type ContentLibraryEntry,
} from '@/data/contentLibrary';
import type { SimpleElement } from '@/types';

interface Props {
  documentMode: 'pdf' | 'word';
  onInsert: (entry: ContentLibraryEntry, elementsOverride?: SimpleElement[]) => void;
}

function previewElements(entry: ContentLibraryEntry, locale: string, fieldLabels?: string[]): SimpleElement[] {
  if (entry.kind === 'block') return entry.buildElements?.({ fieldLabels }) ?? [];
  return buildContentPages(entry, locale)[0]?.elements ?? [];
}

const ContentLibraryPanel: React.FC<Props> = ({ documentMode, onInsert }) => {
  const { t, i18n } = useTranslation('editor');
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState<ContentCategory | 'all'>('all');
  const [preview, setPreview] = useState<ContentLibraryEntry | null>(null);
  const [customFields, setCustomFields] = useState(['First field', 'Second field', 'Third field']);

  const entries = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase(i18n.language);
    return CONTENT_LIBRARY.filter(entry => entry.supportedModes.includes(documentMode))
      .filter(entry => category === 'all' || entry.category === category)
      .filter(entry => {
        if (!needle) return true;
        const searchable = [
          t(`contentLibrary.items.${entry.titleKey}.title`),
          t(`contentLibrary.items.${entry.descriptionKey}`),
          ...entry.keywords,
        ].join(' ').toLocaleLowerCase(i18n.language);
        return searchable.includes(needle);
      });
  }, [category, documentMode, i18n.language, query, t]);

  return (
    <div className="editor-content-library">
      <label className="editor-library-search">
        <FiSearch aria-hidden="true" />
        <input
          value={query}
          onChange={event => setQuery(event.target.value)}
          placeholder={t('contentLibrary.search')}
          aria-label={t('contentLibrary.search')}
        />
      </label>
      <div className="editor-library-filters" role="list" aria-label={t('contentLibrary.categoriesLabel')}>
        <button type="button" className={category === 'all' ? 'is-active' : ''} onClick={() => setCategory('all')}>
          {t('contentLibrary.categories.all')}
        </button>
        {CONTENT_CATEGORIES.map(value => (
          <button key={value} type="button" className={category === value ? 'is-active' : ''} onClick={() => setCategory(value)}>
            {t(`contentLibrary.categories.${value}`)}
          </button>
        ))}
      </div>
      <div className="editor-library-list">
        {entries.map(entry => (
          <article
            key={entry.id}
            className="editor-library-item"
            draggable={entry.kind === 'block'}
            onDragStart={event => {
              event.dataTransfer.setData('application/x-pvad-content-block', entry.id);
              event.dataTransfer.effectAllowed = 'copy';
            }}
          >
            <span className="editor-library-item-icon" aria-hidden="true">
              {entry.kind === 'block' ? <FiGrid /> : <FiFilePlus />}
            </span>
            <span className="editor-library-item-copy">
              <strong>{t(`contentLibrary.items.${entry.titleKey}.title`)}</strong>
              <small>{t(`contentLibrary.items.${entry.descriptionKey}`)}</small>
              <em>{t(`contentLibrary.kinds.${entry.kind}`)}</em>
            </span>
            <span className="editor-library-item-actions">
              <button type="button" onClick={() => setPreview(entry)} title={t('contentLibrary.preview')} aria-label={t('contentLibrary.preview')}>
                <FiEye />
              </button>
              <button type="button" onClick={() => entry.configurable ? setPreview(entry) : onInsert(entry)} title={t('contentLibrary.insert')} aria-label={t('contentLibrary.insert')}>
                <FiFilePlus />
              </button>
            </span>
          </article>
        ))}
        {entries.length === 0 && <p className="editor-library-empty">{t('contentLibrary.empty')}</p>}
      </div>

      {preview && (
        <div className="modal-backdrop" onClick={() => setPreview(null)}>
          <div className="modal-dialog editor-library-preview-dialog" role="dialog" aria-modal="true" onClick={event => event.stopPropagation()}>
            <div className="modal-header">
              <strong>{t(`contentLibrary.items.${preview.titleKey}.title`)}</strong>
              <button className="modal-close" type="button" onClick={() => setPreview(null)} aria-label={t('contentLibrary.close')}><FiX /></button>
            </div>
            <div className="modal-body">
              <div className="editor-library-preview-page" aria-label={t('contentLibrary.preview')}>
                {previewElements(preview, i18n.language, customFields).slice(0, 24).map(element => {
                  const sourceWidth = preview.sourceSize?.width ?? Math.max(520, element.x + element.width);
                  const sourceHeight = preview.sourceSize?.height ?? Math.max(360, element.y + element.height);
                  return <span key={element.id} style={{ left: `${element.x / sourceWidth * 100}%`, top: `${element.y / sourceHeight * 100}%`, width: `${Math.max(2, element.width / sourceWidth * 100)}%`, height: `${Math.max(1, element.height / sourceHeight * 100)}%` }} />;
                })}
              </div>
              <p>{t(`contentLibrary.items.${preview.descriptionKey}`)}</p>
              {preview.configurable && (
                <div className="editor-library-custom-fields">
                  <strong>{t('contentLibrary.fieldLabels')}</strong>
                  {customFields.map((label, index) => (
                    <label key={index}>
                      <input value={label} onChange={event => setCustomFields(values => values.map((value, itemIndex) => itemIndex === index ? event.target.value : value))} aria-label={t('contentLibrary.fieldNumber', { number: index + 1 })} />
                      <button type="button" onClick={() => setCustomFields(values => values.filter((_, itemIndex) => itemIndex !== index))} title={t('contentLibrary.removeField')} aria-label={t('contentLibrary.removeField')}><FiTrash2 /></button>
                    </label>
                  ))}
                  <button type="button" className="editor-library-add-field" onClick={() => setCustomFields(values => [...values, t('contentLibrary.newField', { number: values.length + 1 })])}><FiPlus />{t('contentLibrary.addField')}</button>
                </div>
              )}
              {preview.sourceSize && <p className="editor-library-scaling-note">{t('contentLibrary.scalingNote')}</p>}
            </div>
            <div className="modal-footer">
              <button className="modal-cancel-btn" type="button" onClick={() => setPreview(null)}>{t('contentLibrary.cancel')}</button>
              <button className="modal-confirm-btn" type="button" disabled={preview.configurable && customFields.every(value => !value.trim())} onClick={() => { onInsert(preview, preview.configurable ? preview.buildElements?.({ fieldLabels: customFields }) : undefined); setPreview(null); }}>{t('contentLibrary.insert')}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default ContentLibraryPanel;
