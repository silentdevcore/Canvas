import { assessPasswordStrength } from '@/utils/passwordStrength';

describe('password strength guidance', () => {
  it('does not assess an intentionally empty user password', () => {
    expect(assessPasswordStrength('')).toBeNull();
  });

  it.each([
    ['short', 'weak'],
    ['long-password12', 'fair'],
    ['Longer password 42!', 'strong'],
    ['كلمة مرور عربية طويلة 42!', 'strong'],
  ] as const)('classifies locally without changing the password', (password, expected) => {
    const original = password;

    expect(assessPasswordStrength(password)?.level).toBe(expected);
    expect(password).toBe(original);
  });

  it('reports concrete improvement dimensions', () => {
    expect(assessPasswordStrength('abcdefghijklmnop')).toEqual({
      level: 'fair',
      needsLength: false,
      needsVariety: true,
    });
  });
});
