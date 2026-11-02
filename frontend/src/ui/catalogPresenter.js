import { parseCatalogSearch } from '../application/catalogSearch.js';

export function createCatalogPresenter(useCases, delay = 300) {
  let state = { term: '', inputError: '', status: 'idle', networkError: '', products: [], searchedTerm: '', loaded: false };
  let generation = 0, timer;
  const listeners = new Set();
  const patch = values => { state = { ...state, ...values }; listeners.forEach(fn => fn()); };
  const cancel = () => { clearTimeout(timer); generation += 1; };
  async function load() {
    cancel();
    const request = generation, term = parseCatalogSearch(state.term);
    patch({ status: 'loading', networkError: '' });
    try {
      const products = await useCases.searchProducts(term);
      if (request === generation) patch({ products, searchedTerm: term, status: 'ready', loaded: true });
    } catch (error) {
      if (request === generation) patch({ status: 'error', networkError: error.code === 'auth.session_expired'
        ? 'Inicie sesión nuevamente para consultar el catálogo.' : 'No se pudo consultar el catálogo. Revise la conexión y vuelva a intentar.' });
    }
  }
  return {
    read: () => state,
    subscribe(fn) { listeners.add(fn); return () => listeners.delete(fn); },
    edit(value) {
      cancel();
      try {
        parseCatalogSearch(value);
        patch({ term: value, inputError: '', networkError: '', status: 'loading' });
        timer = setTimeout(load, delay);
      } catch (error) {
        patch({ inputError: error.message, status: state.loaded ? 'ready' : 'idle' });
      }
      return state;
    },
    load,
    dispose: cancel,
  };
}
