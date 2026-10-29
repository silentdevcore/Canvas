import { useCallback, useEffect, useRef, useState } from 'react';
import type { ElementType } from '@/types';
import { ELEMENT_TYPES } from '@/data/elementRegistry';
import { getElementFavorites, replaceElementFavorites } from '@/services/designerElementFavoritesApi';
import { notify } from '@/notifications/toast';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import { readMigratedStorage } from '@/utils/storageMigration';

const cacheKey = (userId: string) => `pvad-designer-element-favorites-v1:${userId}`;

function readCache(key: string): ElementType[] {
  try {
    const values = JSON.parse(readMigratedStorage(localStorage, key,
      key.replace(/^pvad-designer-element-favorites-v1:/, 'pxa-designer-element-favorites-v1:')) ?? '[]');
    return Array.isArray(values)
      ? ELEMENT_TYPES.filter(type => values.includes(type))
      : [];
  } catch {
    return [];
  }
}

function cache(key: string, values: readonly ElementType[]): void {
  try {
    localStorage.setItem(key, JSON.stringify(values));
  } catch {
    // The server is authoritative; an unavailable cache must not fail a save.
  }
}

export function useElementFavorites(errorMessage: string) {
  const { user } = useDesignerAuth();
  const key = cacheKey(user.id);
  const [state, setState] = useState(() => ({ key, values: readCache(key) }));
  const favorites = state.key === key ? state.values : readCache(key);
  const owner = useRef(key);
  owner.current = key;
  const setFavorites = (values: ElementType[]) => setState({ key, values });
  const [loading, setLoading] = useState(true);
  const [offline, setOffline] = useState(false);
  const current = useRef(favorites);
  const changedLocally = useRef(false);
  const mutationVersion = useRef(0);
  const saveQueue = useRef<Promise<unknown>>(Promise.resolve());
  current.current = favorites;

  useEffect(() => {
    let active = true;
    changedLocally.current = false;
    mutationVersion.current++;
    saveQueue.current = Promise.resolve();
    setFavorites(readCache(key));
    setLoading(true);
    setOffline(false);
    getElementFavorites()
      .then(values => {
        if (!active) return;
        if (!changedLocally.current) {
          setFavorites(values);
          cache(key, values);
        }
        setOffline(false);
      })
      .catch(() => { if (active) setOffline(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [key]);

  const toggle = useCallback(async (type: ElementType) => {
    const previous = current.current;
    changedLocally.current = true;
    const version = ++mutationVersion.current;
    const selected = previous.includes(type);
    const next = ELEMENT_TYPES.filter(value => value !== type
      ? previous.includes(value)
      : !selected);
    setFavorites(next);
    cache(key, next);
    saveQueue.current = saveQueue.current
      .catch(() => undefined)
      .then(() => {
        if (owner.current !== key) return next;
        return replaceElementFavorites(next);
      });
    try {
      const saved = await saveQueue.current as ElementType[];
      if (owner.current === key && mutationVersion.current === version) {
        setFavorites(saved);
        cache(key, saved);
      }
    } catch {
      if (owner.current === key && mutationVersion.current === version) {
        setFavorites(previous);
        cache(key, previous);
        notify.error(errorMessage);
      }
    }
  }, [errorMessage, key]);

  return { favorites, loading, offline, toggle };
}
