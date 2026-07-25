import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';
import {
  currentDesignerUser,
  describeDesignerAuthError,
  DesignerAuthError,
  type DesignerUser,
  exchangeCallback,
  redirectToAccount,
  shouldRedirectToAccount,
} from './designerAuth';

type GateState =
  | { kind: 'loading' }
  | { kind: 'ready'; user: DesignerUser }
  | {
      kind: 'error';
      title: string;
      message: string;
      retry: boolean;
      openAccount: boolean;
    };

interface DesignerAuthContextValue {
  user: DesignerUser;
  setUser: (user: DesignerUser) => void;
  refreshUser: () => Promise<DesignerUser>;
}

const DesignerAuthContext = createContext<DesignerAuthContextValue | null>(null);

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
        const presentation = describeDesignerAuthError(error);
        setState({
          kind: 'error',
          ...presentation,
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
      <main className="designer-auth-state">
        <section>
          <p className="designer-auth-kicker">Power Dox Automation</p>
          <h1>{state.title}</h1>
          <p>{state.message}</p>
          <div className="designer-auth-actions">
            {state.retry && <button type="button" onClick={() => location.reload()}>Try again</button>}
            {state.openAccount && (
              <button type="button" className="secondary" onClick={() => void redirectToAccount()}>
                Open PXA Account
              </button>
            )}
          </div>
        </section>
      </main>
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
