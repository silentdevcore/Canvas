import React from 'react';
import { FiAlertTriangle, FiLoader, FiX } from 'react-icons/fi';
import type { ProjectImportState } from '@/hooks/useProjectImportFlow';

interface ProjectImportNoticeProps {
  state: ProjectImportState | null;
  labels: {
    opening: string;
    review: string;
    failed: string;
    uploading: string;
    validating: string;
    assets: string;
    applying: string;
    cancel: string;
    dismiss: string;
    openAnyway: string;
    keepCurrent: string;
    preserved: string;
    missingFeature: (feature: string) => string;
  };
  onCancel: () => void;
  onDismiss: () => void;
  onReviewDecision: (accepted: boolean) => void;
}

const ProjectImportNotice: React.FC<ProjectImportNoticeProps> = ({
  state, labels, onCancel, onDismiss, onReviewDecision,
}) => {
  if (!state) return null;
  const phaseLabel = state.kind === 'progress' ? labels[state.phase] : '';

  return (
    <aside
      className={`project-import-notice is-${state.kind}`}
      role={state.kind === 'error' ? 'alert' : 'status'}
      aria-live={state.kind === 'error' ? 'assertive' : 'polite'}
    >
      <header>
        {state.kind === 'progress' ? <FiLoader className="is-spinning" aria-hidden="true" /> : <FiAlertTriangle aria-hidden="true" />}
        <strong>{state.kind === 'progress' ? labels.opening : state.kind === 'review' ? labels.review : labels.failed}</strong>
        {state.kind === 'error' && (
          <button type="button" className="project-import-close" title={labels.dismiss} onClick={onDismiss}><FiX /></button>
        )}
      </header>

      {state.kind === 'progress' && (
        <>
          <div className="project-import-progress-row"><span>{phaseLabel}</span><span>{state.percent}%</span></div>
          <div className="project-import-progress" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={state.percent}>
            <span style={{ width: `${state.percent}%` }} />
          </div>
          <p>{labels.preserved}</p>
          {state.cancellable && <button type="button" onClick={onCancel}>{labels.cancel}</button>}
        </>
      )}

      {state.kind === 'review' && (
        <>
          <p>{labels.preserved}</p>
          <ul>
            {state.compatibility.missingFeatures.map(feature => <li key={`feature-${feature}`}>{labels.missingFeature(feature)}</li>)}
            {state.compatibility.diagnostics.map(diagnostic => (
              <li key={`${diagnostic.code}-${diagnostic.source ?? ''}`}>
                <code>{diagnostic.code}</code> {diagnostic.message}{diagnostic.source ? ` (${diagnostic.source})` : ''}
              </li>
            ))}
          </ul>
          <div className="project-import-actions">
            <button type="button" onClick={() => onReviewDecision(false)}>{labels.keepCurrent}</button>
            <button type="button" className="is-primary" onClick={() => onReviewDecision(true)}>{labels.openAnyway}</button>
          </div>
        </>
      )}

      {state.kind === 'error' && (
        <>
          <p>{state.message}</p>
          {state.code && <code className="project-import-code">{state.code}</code>}
          <p>{labels.preserved}</p>
        </>
      )}
    </aside>
  );
};

export default ProjectImportNotice;
