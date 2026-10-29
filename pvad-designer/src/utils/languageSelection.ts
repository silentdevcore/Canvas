export function updateLanguageSelection(
  current: string[],
  language: string,
  selected: boolean,
): string[] {
  if (selected) {
    return current.includes(language) ? current : [...current, language];
  }

  return current.filter(value => value !== language);
}
