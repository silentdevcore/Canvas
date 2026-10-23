export type PasswordStrengthLevel = 'weak' | 'fair' | 'strong';

export interface PasswordStrengthAssessment {
  level: PasswordStrengthLevel;
  needsLength: boolean;
  needsVariety: boolean;
}

/** Local guidance only. The password never leaves the caller and is not retained. */
export const assessPasswordStrength = (password: string): PasswordStrengthAssessment | null => {
  if (!password) return null;

  const classes = new Set<string>();
  if (/\p{Ll}/u.test(password)) classes.add('lower');
  if (/\p{Lu}/u.test(password)) classes.add('upper');
  if (/\p{L}/u.test(password) && !/[\p{Ll}\p{Lu}]/u.test(password)) classes.add('letter');
  if (/\p{N}/u.test(password)) classes.add('number');
  if (/[^\p{L}\p{N}\s]/u.test(password)) classes.add('symbol');
  if (/\s/u.test(password)) classes.add('space');

  const length = Array.from(password).length;
  const needsLength = length < 16;
  const needsVariety = classes.size < 3;
  const level: PasswordStrengthLevel = !needsLength && !needsVariety
    ? 'strong'
    : length >= 16 || (length >= 12 && classes.size >= 2)
      ? 'fair'
      : 'weak';

  return { level, needsLength, needsVariety };
};
