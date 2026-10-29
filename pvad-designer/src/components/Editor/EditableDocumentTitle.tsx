import React, { useEffect, useRef, useState } from 'react';
import { FiEdit3 } from 'react-icons/fi';

interface EditableDocumentTitleProps {
  name: string;
  inputLabel: string;
  actionLabel: string;
  hint: string;
  validationMessage: string;
  onRename: (name: string) => void;
}

export const EditableDocumentTitle: React.FC<EditableDocumentTitleProps> = ({
  name,
  inputLabel,
  actionLabel,
  hint,
  validationMessage,
  onRename,
}) => {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(name);
  const [error, setError] = useState('');
  const skipBlur = useRef(false);

  useEffect(() => {
    if (!editing) setDraft(name);
  }, [editing, name]);

  const commit = () => {
    const normalized = draft.trim();
    if (normalized.length < 1 || normalized.length > 200) {
      setError(validationMessage);
      return;
    }
    if (normalized !== name) onRename(normalized);
    setDraft(normalized);
    setError('');
    setEditing(false);
  };

  const cancel = () => {
    skipBlur.current = true;
    setDraft(name);
    setError('');
    setEditing(false);
  };

  if (!editing) {
    return (
      <button
        type="button"
        className="editor-document-title"
        onClick={() => {
          skipBlur.current = false;
          setEditing(true);
        }}
        aria-label={actionLabel}
        title={hint}
      >
        <span>{name}</span>
        <FiEdit3 aria-hidden="true" />
      </button>
    );
  }

  return (
    <div className="editor-document-title-editor">
      <input
        autoFocus
        value={draft}
        maxLength={200}
        aria-label={inputLabel}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? 'editor-document-title-error' : undefined}
        onChange={event => {
          setDraft(event.currentTarget.value);
          setError('');
        }}
        onBlur={() => {
          if (skipBlur.current) {
            skipBlur.current = false;
            return;
          }
          commit();
        }}
        onKeyDown={event => {
          if (event.key === 'Enter') commit();
          if (event.key === 'Escape') {
            event.preventDefault();
            cancel();
          }
        }}
      />
      {error && <span id="editor-document-title-error" role="alert">{error}</span>}
    </div>
  );
};
