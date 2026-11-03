export function parseHistorySearch(value = '') {
  if (typeof value !== 'string' || value.length > 100 || /[\p{C}\p{Zl}\p{Zp}]/u.test(value)) {
    const error = new Error('Use hasta 100 caracteres de la referencia, sin controles ni saltos de línea.');
    error.code = 'history.search_invalid';
    throw error;
  }
  return value.trim();
}
