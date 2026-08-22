import { useCallback, useEffect, useRef, useState } from 'react';
import type { ElementType } from '@/types';
import { ELEMENT_TYPES } from '@/data/elementRegistry';
import { getElementFavorites, replaceElementFavorites } from '@/services/designerElementFavoritesApi';
import { notify } from '@/notifications/toast';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';

const cacheKey = (userId: string) => `pxa-designer-element-favorites-v1:${userId}`;

function readCache(key: string): ElementType[] {
  try {
    const values = JSON.parse(localStorage.getItem(key) ?? '[]');
    return Array.isArray(values)
      ? ELEMENT_TYPES.filter(type => values.includes(type))
      : [];
  } catch {
    return [];
  }
}

function cache(key: string, values: readonly ElementType[]): void {
  localStorage.setItem(key, JSON.stringify(values));
}

export function useElementFavorites(errorMessage: string) {
  const { user } = useDesignerAuth();
  const key = cacheKey(user.id);
  const [favorites, setFavorites] = useState<ElementType[]>(() => readCache(key));
  const [loading, setLoading] = useState(true);
  const [offline, setOffline] = useState(false);
  const current = useRef(favorites);
  const changedLocally = useRef(false);
  const mutationVersion = useRef(0);
  const saveQueue = useRef<Promise<unknown>>(Promise.resolve());
  current.current = favorites;

  useEffect(() => {
    let active = true;
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
      .then(() => replaceElementFavorites(next));
    try {
      const saved = await saveQueue.current as ElementType[];
      if (mutationVersion.current === version) {
        setFavorites(saved);
        cache(key, saved);
      }
    } catch {
      if (mutationVersion.current === version) {
        setFavorites(previous);
        cache(key, previous);
        notify.error(errorMessage);
      }
    }
  }, [errorMessage, key]);

  return { favorites, loading, offline, toggle };
}
