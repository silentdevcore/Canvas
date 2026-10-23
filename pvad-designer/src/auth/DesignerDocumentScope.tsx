import { useEffect, useState, type PropsWithChildren } from 'react';
import type { DesignerUser } from './designerAuth';
import { setDocumentHandoffOwner } from './documentHandoff';

/** Keep document routes unmounted until their local workbook context is selected. */
export default function DesignerDocumentScope({ user, children }: PropsWithChildren<{ user: DesignerUser }>) {
  const scope = JSON.stringify([user.id, user.activeOrganizationId]);
  const [ready, setReady] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let active = true;
    let reset: (() => void) | undefined;
    setDocumentHandoffOwner(user.id, user.activeOrganizationId);
    setFailed(false);
    void import('../spreadsheet/store').then(async ({ configureSpreadsheetOwner }) => {
      if (!active) return;
      reset = () => { void configureSpreadsheetOwner(null, null); };
      await configureSpreadsheetOwner(user.id, user.activeOrganizationId);
      if (active) setReady(scope);
    }).catch(() => { if (active) setFailed(true); });
    return () => { active = false; setDocumentHandoffOwner(null, null); reset?.(); };
  }, [scope, user.id, user.activeOrganizationId]);
  if (failed) return <main role="alert">The local document workspace could not be opened. Reload to retry.</main>;
  if (ready !== scope) return <main aria-busy="true">Loading workspace...</main>;
  return <>{children}</>;
}
