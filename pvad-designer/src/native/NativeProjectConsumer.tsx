import { useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import { notify } from '@/notifications/toast';
import { takeNativeProject, type NativeProjectKind } from './nativeProjectLaunch';

export default function NativeProjectConsumer({ kind, onFile }: { kind: NativeProjectKind; onFile: (file: File) => Promise<void> }) {
  const [params, setParams] = useSearchParams();
  const { user } = useDesignerAuth();
  const { t } = useTranslation('nativeProject');
  const started = useRef<string | null>(null);
  const handler = useRef(onFile);
  handler.current = onFile;
  const id = params.get('nativeLaunch');
  useEffect(() => {
    if (!id || started.current === id) return;
    let active = true;
    // Let parent import-flow mount cleanup finish before consuming the one-use file.
    void Promise.resolve().then(() => {
      if (!active || started.current === id) return;
      started.current = id;
      const file = takeNativeProject(id, user, kind);
      const next = new URLSearchParams(params);
      next.delete('nativeLaunch');
      setParams(next, { replace: true });
      if (file) void handler.current(file).catch(() => notify.error(t('failed')));
      else notify.error(t('expired'));
    });
    return () => { active = false; };
  }, [id, kind, params, setParams, t, user]);
  return null;
}
