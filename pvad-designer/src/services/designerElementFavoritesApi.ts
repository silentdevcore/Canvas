import type { ElementType } from '@/types';

const endpoint = '/api/pvad/v1/designer/preferences/element-favorites';

interface FavoritesResponse {
  elementTypes: ElementType[];
}

async function request(init?: RequestInit): Promise<ElementType[]> {
  const response = await fetch(endpoint, {
    credentials: 'include',
    headers: { Accept: 'application/json', ...init?.headers },
    ...init,
  });
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.detail || body?.title || 'Element favorites are unavailable.');
  }
  return (body as FavoritesResponse).elementTypes;
}

export const getElementFavorites = (): Promise<ElementType[]> => request();

export const replaceElementFavorites = (elementTypes: readonly ElementType[]): Promise<ElementType[]> =>
  request({
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ elementTypes }),
  });
