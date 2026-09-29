import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';
import {
  currentDesignerUser,
  describeDesignerAuthError,
  DesignerAuthError,
  type DesignerUser,
  exchangeCallback,
  isKnownDesignerAuthCode,
  redirectToAccount,
  shouldRedirectToAccount,
} from './designerAuth';
import StatusPage from '@/components/StatusPage';
import type { PxaStatusKey } from '../../../websites/shared/statusPages';
import { recordBrowserUnknownDiagnosticMapping } from '../../../websites/shared/browserTelemetry.js';

type GateState =
  | { kind: 'loading' }
  | { kind: 'ready'; user: DesignerUser }
  | {
      kind: 'error';
      title: string;
      message: string;
      retry: boolean;
      openAccount: boolean;
      statusKey: PxaStatusKey;
      diagnosticCode?: string;
    };

interface DesignerAuthContextValue {
  user: DesignerUser;
  setUser: (user: DesignerUser) => void;
  refreshUser: () => Promise<DesignerUser>;
}

const DesignerAuthContext = createContext<DesignerAuthContextValue | null>(null);

function authStatusKey(error: DesignerAuthError): PxaStatusKey {
  if (error.offline) return 'offline';
  switch (error.code) {
    case 'PXA_SUBSCRIPTION_CANCELLED':
    case 'PXA_SUBSCRIPTION_INACTIVE':
    case 'PXA_TRIAL_EXPIRED':
    case 'PXA_GRACE_PERIOD_EXPIRED':
    case 'PXA_ENTITLEMENT_EXPIRED':
      return 'trialExpired';
    case 'PXA_ENTITLEMENT_MISSING':
    case 'PXA_ENTITLEMENT_DENIED':
      return 'entitlementDenied';
    case 'PXA_DESIGNER_ACCOUNT_DISABLED':
    case 'PXA_DESIGNER_MEMBERSHIP_INACTIVE':
    case 'PXA_ORGANIZATION_INACTIVE':
    case 'PXAAPI015':
    case 'PXAAPI016':
      return 'suspended';
    case 'PXA_DESIGNER_SESSION_EXPIRED':
      return 'sessionExpired';
    case 'PXA_API_VERSION_INCOMPATIBLE':
      return 'incompatibleVersion';
    default:
      return error.status === 403 ? 'forbidden' : 'unavailable';
  }
}

export const DesignerAuthContextProvider: React.FC<React.PropsWithChildren<{ user: DesignerUser }>> = ({
  user: initialUser,
  children,
}) => {
  const [user, setUser] = useState(initialUser);
  const value = useMemo<DesignerAuthContextValue>(() => ({
    user,
    setUser,
    refreshUser: async () => user,
  }), [user]);
  return <DesignerAuthContext.Provider value={value}>{children}</DesignerAuthContext.Provider>;
};

export function useDesignerAuth(): DesignerAuthContextValue {
  const value = useContext(DesignerAuthContext);
  if (!value)
    throw new Error('useDesignerAuth must be used inside DesignerAuthGate.');
  return value;
}

const DesignerAuthGate: React.FC<React.PropsWithChildren> = ({ children }) => {
  const [state, setState] = useState<GateState>({ kind: 'loading' });

  useEffect(() => {
    let active = true;
    const bootstrap = async () => {
      try {
        if (location.pathname === '/auth/callback') {
          const returnPath = await exchangeCallback();
          location.replace(returnPath);
          return;
        }
        const user = await currentDesignerUser();
        if (active) setState({ kind: 'ready', user });
      } catch (caught) {
        const error = caught as DesignerAuthError;
        if (shouldRedirectToAccount(location.pathname, error)) {
          await redirectToAccount();
          return;
        }
        if (!active) return;
        if (!isKnownDesignerAuthCode(error.code)) recordBrowserUnknownDiagnosticMapping();
        const presentation = describeDesignerAuthError(error);
        setState({
          kind: 'error',
          ...presentation,
          statusKey: authStatusKey(error),
          diagnosticCode: error.code,
        });
      }
    };
    void bootstrap();
    return () => { active = false; };
  }, []);

  const context = useMemo<DesignerAuthContextValue | null>(() => {
    if (state.kind !== 'ready') return null;
    return {
      user: state.user,
      setUser: user => setState({ kind: 'ready', user }),
      refreshUser: async () => {
        const user = await currentDesignerUser();
        setState({ kind: 'ready', user });
        return user;
      },
    };
  }, [state]);

  if (state.kind === 'ready' && context) {
    return (
      <DesignerAuthContext.Provider value={context}>
        {children}
      </DesignerAuthContext.Provider>
    );
  }
  if (state.kind === 'error') {
    return (
      <StatusPage
        statusKey={state.statusKey}
        title={state.title}
        message={state.message}
        diagnosticCode={state.diagnosticCode}
        primaryAction={state.retry ? 'retry' : state.openAccount ? 'account' : undefined}
      />
    );
  }
  return (
    <main className="designer-auth-state" aria-busy="true">
      <section>
        <span className="designer-auth-spinner" aria-hidden="true" />
        <p className="designer-auth-kicker">Power Dox Automation</p>
        <h1>{location.pathname === '/auth/callback' ? 'Completing sign in' : 'Checking Designer access'}</h1>
      </section>
    </main>
  );
};

export default DesignerAuthGate;
