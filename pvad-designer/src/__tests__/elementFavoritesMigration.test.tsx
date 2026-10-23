/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { useElementFavorites } from '../hooks/useElementFavorites';
import { getElementFavorites, replaceElementFavorites } from '../services/designerElementFavoritesApi';

let mockUserId = 'a';
let root: Root;
(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
function renderHook(callback: () => ReturnType<typeof useElementFavorites>) {
  const result = { current: undefined as unknown as ReturnType<typeof useElementFavorites> };
  function Probe() { result.current = callback(); return null; }
  root = createRoot(document.createElement('div'));
  const rerender = () => act(() => root.render(<Probe />));
  rerender();
  return { result, rerender };
}
afterEach(() => act(() => root?.unmount()));
const flush = () => act(async () => { await Promise.resolve(); });
jest.mock('../auth/DesignerAuthGate', () => ({
  useDesignerAuth: () => ({ user: { id: mockUserId } }),
}));
jest.mock('../services/designerElementFavoritesApi', () => ({
  getElementFavorites: jest.fn(), replaceElementFavorites: jest.fn(),
}));
jest.mock('../notifications/toast', () => ({ notify: { error: jest.fn() } }));

beforeEach(() => {
  localStorage.clear();
  jest.clearAllMocks();
  mockUserId = 'a';
  jest.mocked(getElementFavorites).mockRejectedValue(new Error('offline'));
});

test('migrates only the current user cache and isolates account switching', async () => {
  localStorage.setItem('pxa-designer-element-favorites-v1:a', '["text"]');
  localStorage.setItem('pxa-designer-element-favorites-v1:b', '["image"]');
  const { result, rerender } = renderHook(() => useElementFavorites('failed'));
  await flush();
  expect(result.current.loading).toBe(false);
  expect(result.current.favorites).toEqual(['text']);
  expect(localStorage.getItem('pxa-designer-element-favorites-v1:a')).toBeNull();
  expect(localStorage.getItem('pxa-designer-element-favorites-v1:b')).not.toBeNull();
  mockUserId = 'b';
  rerender();
  expect(result.current.favorites).toEqual(['image']);
  await flush();
  expect(result.current.loading).toBe(false);
});

test('an old mutation result cannot overwrite the next account', async () => {
  let finish!: (values: any) => void;
  jest.mocked(replaceElementFavorites).mockImplementation(() => new Promise(resolve => { finish = resolve; }));
  const { result, rerender } = renderHook(() => useElementFavorites('failed'));
  await flush();
  let mutation!: Promise<void>;
  act(() => { mutation = result.current.toggle('text'); });
  await flush();
  expect(replaceElementFavorites).toHaveBeenCalled();
  mockUserId = 'b';
  rerender();
  await flush();
  await act(async () => { finish(['text']); await mutation; });
  expect(result.current.favorites).toEqual([]);
  expect(localStorage.getItem('pvad-designer-element-favorites-v1:b')).toBeNull();
});
