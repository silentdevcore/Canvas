import { useCallback, useEffect, useRef, useState } from 'react';
import type {
  DesignerProjectApiError,
  ProjectCompatibility,
  ProjectTransferPhase,
  ProjectTransferProgress,
} from '@/services/designerProjectApi';
import { isProjectOperationCancelled } from '@/services/designerProjectApi';

export type ProjectImportPhase = ProjectTransferPhase | 'assets' | 'applying';

export type ProjectImportState =
  | { kind: 'progress'; phase: ProjectImportPhase; percent: number; cancellable: boolean }
  | { kind: 'review'; compatibility: ProjectCompatibility }
  | { kind: 'error'; code?: string; message: string };

export const useProjectImportFlow = () => {
  const [state, setState] = useState<ProjectImportState | null>(null);
  const controller = useRef<AbortController | null>(null);
  const reviewDecision = useRef<((accepted: boolean) => void) | null>(null);

  const begin = useCallback(() => {
    controller.current?.abort();
    reviewDecision.current?.(false);
    const next = new AbortController();
    controller.current = next;
    setState({ kind: 'progress', phase: 'uploading', percent: 0, cancellable: true });
    return next.signal;
  }, []);

  const report = useCallback((progress: ProjectTransferProgress) => {
    setState({ kind: 'progress', phase: progress.phase, percent: progress.percent, cancellable: true });
  }, []);

  const stage = useCallback((phase: ProjectImportPhase, percent: number) => {
    setState({ kind: 'progress', phase, percent, cancellable: false });
  }, []);

  const review = useCallback((compatibility: ProjectCompatibility): Promise<boolean> => {
    if (!compatibility.requiresReview) return Promise.resolve(true);
    setState({ kind: 'review', compatibility });
    return new Promise(resolve => {
      reviewDecision.current = resolve;
    });
  }, []);

  const decideReview = useCallback((accepted: boolean) => {
    reviewDecision.current?.(accepted);
    reviewDecision.current = null;
    if (!accepted) setState(null);
  }, []);

  const fail = useCallback((error: unknown, fallback: string) => {
    const projectError = error as Partial<DesignerProjectApiError> | undefined;
    if (isProjectOperationCancelled(error)) {
      setState(null);
      return;
    }
    setState({
      kind: 'error',
      code: projectError?.code,
      message: error instanceof Error ? error.message : fallback,
    });
  }, []);

  const finish = useCallback(() => {
    controller.current = null;
    reviewDecision.current = null;
    setState(null);
  }, []);

  const cancel = useCallback(() => {
    controller.current?.abort();
    controller.current = null;
    decideReview(false);
    setState(null);
  }, [decideReview]);

  useEffect(() => () => {
    controller.current?.abort();
    reviewDecision.current?.(false);
  }, []);

  return { state, begin, report, stage, review, decideReview, fail, finish, cancel, dismiss: finish };
};
