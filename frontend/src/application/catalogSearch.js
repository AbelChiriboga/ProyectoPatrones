// Query policy, not a restriction on product names. Mirrors the backend's 100-character query limit.
export const MAX_SEARCH_LENGTH = 100;
export class CatalogSearchError extends Error {
  constructor(code, message) { super(message); this.code = code; }
}
export function parseCatalogSearch(value = '') {
  if (typeof value !== 'string') throw new CatalogSearchError('catalog.invalid_query', 'Ingrese un nombre o código de producto.');
  if (value.length > MAX_SEARCH_LENGTH) throw new CatalogSearchError('catalog.query_too_long', `La búsqueda admite hasta ${MAX_SEARCH_LENGTH} caracteres. No se ha recortado el texto.`);
  if (/[\p{C}\p{Zl}\p{Zp}]/u.test(value)) throw new CatalogSearchError('catalog.invalid_characters', 'La búsqueda no admite caracteres de control, invisibles ni saltos de línea.');
  const term = value.trim();
  if (term && !/[\p{L}\p{N}]/u.test(term)) throw new CatalogSearchError('catalog.query_noise', 'Incluya letras o números de un nombre o código; no solo símbolos.');
  return term;
}
