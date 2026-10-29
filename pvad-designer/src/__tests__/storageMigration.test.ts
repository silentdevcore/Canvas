/** @jest-environment jsdom */
import { readMigratedStorage } from '../utils/storageMigration';

describe('browser key migration', () => {
  beforeEach(() => localStorage.clear());

  test('moves a legacy preference once', () => {
    localStorage.setItem('old', '1');
    expect(readMigratedStorage(localStorage, 'new', 'old')).toBe('1');
    expect(localStorage.getItem('new')).toBe('1');
    expect(localStorage.getItem('old')).toBeNull();
    expect(readMigratedStorage(localStorage, 'new', 'old')).toBe('1');
  });

  test('canonical value wins, including an empty value', () => {
    localStorage.setItem('new', '');
    localStorage.setItem('old', '1');
    expect(readMigratedStorage(localStorage, 'new', 'old')).toBe('');
    expect(localStorage.getItem('old')).toBe('1');
  });

  test('failed writes retain the original', () => {
    const removeItem = jest.fn();
    const storage = { getItem: (key: string) => key === 'old' ? '1' : null,
      setItem: () => { throw new Error('quota'); }, removeItem };
    expect(readMigratedStorage(storage, 'new', 'old')).toBe('1');
    expect(removeItem).not.toHaveBeenCalled();
  });

  test('does not inspect another user key', () => {
    localStorage.setItem('old:b', 'private');
    expect(readMigratedStorage(localStorage, 'new:a', 'old:a')).toBeNull();
    expect(localStorage.getItem('old:b')).toBe('private');
  });
});
