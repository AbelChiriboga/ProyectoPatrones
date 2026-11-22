import { parseCatalogSearch } from '../application/catalogSearch.js';

export function createCatalogPresenter(useCases, delay = 300) {
  let state = { term: '', inputError: '', status: 'idle', networkError: '', products: [], searchedTerm: '', loaded: false };
  let generation = 0, timer;
  const cache = new Map();
  const listeners = new Set();
  const patch = values => { state = { ...state, ...values }; listeners.forEach(fn => fn()); };
  const cancel = () => { clearTimeout(timer); generation += 1; };
  async function load() {
    cancel();
    const request = generation, term = parseCatalogSearch(state.term);
    patch({ status: 'loading', networkError: '' });
    try {
      const products = await useCases.searchProducts(term);
      cache.set(term, products);
      if (request === generation) patch({ products, searchedTerm: term, status: 'ready', loaded: true });
    } catch (error) {
      if (request === generation) {
        // Resiliencia: si la red falla temporalmente pero existe en caché, restaurar caché local
        const cached = cache.get(term);
        if (cached && cached.length) {
          patch({ products: cached, searchedTerm: term, status: 'ready', networkError: 'Conexión inestable. Mostrando catálogo desde caché local.' });
        } else {
          patch({ status: 'error', networkError: error.code === 'auth.session_expired'
            ? 'Inicie sesión nuevamente para consultar el catálogo.' : 'No se pudo consultar el catálogo. Revise la conexión y vuelva a intentar.' });
        }
      }
    }
  }
  return {
    read: () => state,
    subscribe(fn) { listeners.add(fn); return () => listeners.delete(fn); },
    edit(value) {
      cancel();
      try {
        const parsed = parseCatalogSearch(value);
        // Respuesta instantánea: si ya está en caché local, precargar productos inmediatamente
        const cached = cache.get(parsed);
        patch({
          term: value,
          inputError: '',
          networkError: '',
          status: 'loading',
          ...(cached ? { products: cached } : {})
        });
        timer = setTimeout(load, delay);
      } catch (error) {
        patch({ inputError: error.message, status: state.loaded ? 'ready' : 'idle' });
      }
      return state;
    },
    load,
    dispose: cancel,
    // Búsqueda instantánea por código de barras o SKU en la caché local
    findExactMatch(query) {
      if (!query || typeof query !== 'string') return null;
      const normalized = query.trim().toLowerCase();
      // Buscar en los productos actuales
      const inCurrent = state.products.find(p =>
        (p.code && p.code.toLowerCase() === normalized) || p.name.toLowerCase() === normalized
      );
      if (inCurrent) return inCurrent;

      // Buscar en todos los productos almacenados en caché
      for (const list of cache.values()) {
        const match = list.find(p =>
          (p.code && p.code.toLowerCase() === normalized) || p.name.toLowerCase() === normalized
        );
        if (match) return match;
      }
      return null;
    },
    getCacheSize: () => cache.size,
    clearCache: () => cache.clear(),
  };
}
