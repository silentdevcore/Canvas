import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FiFolder, FiLogIn, FiRefreshCw, FiX } from 'react-icons/fi';
import { currentDesignerUser, type DesignerUser } from '@/auth/designerAuth';
import { canImportNativeProject, nativeProjectInbox, nativeProjectKind, NativeProjectError, stageNativeProject } from './nativeProjectLaunch';
import './native-project.css';

export default function NativeProjectLaunchPage() {
  const { t } = useTranslation('nativeProject');
  const navigate = useNavigate();
  const inbox = useSyncExternalStore(nativeProjectInbox.subscribe, nativeProjectInbox.getSnapshot);
  const [user, setUser] = useState<DesignerUser | null>(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const alive = useRef(true);
  const checking = useRef(false);
  const input = useRef<HTMLInputElement>(null);
  const refresh = useCallback(async () => {
    if (checking.current || busyRef.current) return;
    checking.current = true;
    try {
      const result = await currentDesignerUser();
      if (alive.current) { setUser(result); setMessage(''); }
    } catch (error) {
      if (alive.current) {
        setUser(null);
        setMessage((error as { status?: number }).status === 401 ? '' : t('accessDenied'));
      }
    } finally { checking.current = false; }
  }, [t]);
  useEffect(() => {
    alive.current = true;
    void refresh();
    const onFocus = () => { void refresh(); };
    window.addEventListener('focus', onFocus);
    return () => { alive.current = false; window.removeEventListener('focus', onFocus); };
  }, [refresh]);

  const open = async () => {
    if (!user || busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setMessage('');
    try {
      const active = await currentDesignerUser();
      if (active.id !== user.id || active.activeOrganizationId !== user.activeOrganizationId)
        throw new NativeProjectError('accountChanged');
      if (!inbox.name || !canImportNativeProject(active, nativeProjectKind(inbox.name))) {
        setMessage(t('permission')); return;
      }
      const file = await nativeProjectInbox.read();
      const confirmed = await currentDesignerUser();
      if (!alive.current || confirmed.id !== active.id || confirmed.activeOrganizationId !== active.activeOrganizationId)
        throw new NativeProjectError('accountChanged');
      const id = stageNativeProject(file, confirmed);
      nativeProjectInbox.clear();
      navigate(`/${nativeProjectKind(file.name) === 'pdf' ? 'pdf' : 'spreadsheet'}/create?nativeLaunch=${encodeURIComponent(id)}`);
    } catch (error) {
      if (alive.current) setMessage(t(error instanceof NativeProjectError ? error.reason : 'failed'));
    } finally { busyRef.current = false; if (alive.current) setBusy(false); }
  };

  return <main className="designer-auth-state native-project-launch">
    <section aria-busy={busy}>
      <p className="designer-auth-kicker">PVADE</p>
      <h1>{t('title')}</h1>
      <p className="native-project-name">{inbox.name ?? t('noFile')}</p>
      {user ? <p>{user.displayName} · {user.organizations.find(org => org.id === user.activeOrganizationId)?.name ?? ''}</p>
        : <p>{t('requiresSignIn')}</p>}
      {(message || inbox.error) && <p role="alert">{message || t(inbox.error!)}</p>}
      <div className="native-project-actions">
        {!user && <a href="/pdf/template" target="_blank" rel="noopener noreferrer"><FiLogIn aria-hidden="true" />{t('login')}</a>}
        <button type="button" disabled={busy} onClick={() => { void refresh(); }}><FiRefreshCw aria-hidden="true" />{t('refresh')}</button>
        <button type="button" disabled={busy || Boolean(inbox.name)} onClick={() => input.current?.click()}><FiFolder aria-hidden="true" />{t('choose')}</button>
        <button type="button" disabled={busy || !inbox.name || !user} onClick={() => { void open(); }}><FiFolder aria-hidden="true" />{t('open')}</button>
        <button type="button" disabled={busy || !inbox.name} onClick={nativeProjectInbox.clear}><FiX aria-hidden="true" />{t('discard')}</button>
      </div>
      <input ref={input} type="file" hidden accept=".pvadp,.pvads" onChange={event => {
        const file = event.target.files?.[0]; event.target.value = '';
        if (file) nativeProjectInbox.enqueue([{ kind: 'file', name: file.name, getFile: async () => file }]);
      }} />
    </section>
  </main>;
}
