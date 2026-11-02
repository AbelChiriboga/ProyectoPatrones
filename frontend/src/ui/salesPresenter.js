import { parseHistorySearch } from '../application/historyQuery.js';

// Observable UI state; use cases own API contracts, persistence and idempotency.
export const saleKey = sale => JSON.stringify(sale);
export function createSalesPresenter(useCases, onConfirmed = () => {}) {
  let sale = null, activeView = null, historyGeneration = 0, detailGeneration = 0;
  let lastHistoryRequest = { cursor: 0, query: '', cursors: [0], index: 0 };
  let state = { key: '', quote: null, method: '', reviewAccepted: false, busy: '', error: '', message: '', notice: '', outcome: 'idle',
    pending: useCases.pendingSale(), history: [], historyLoaded: false, historyStatus: 'idle', historyError: '', historyTerm: '', historySearchError: '', historyQuery: '', historyCursors: [0], historyIndex: 0, historyHasMore: false, historyNextBeforeId: null, detail: null, detailOpen: false, detailId: null, detailStatus: 'idle', detailError: '', resultSale: null };
  const listeners = new Set();
  const patch = changes => { state = { ...state, ...changes }; listeners.forEach(fn => fn()); };
  const uncertain = 'Resultado incierto. No vuelva a cobrar; consulte la operación pendiente con el mismo identificador.';
  function failure(error, readOnly = false) {
    if (!readOnly && useCases.pendingSale()) return uncertain;
    if (error.code?.startsWith('session.')) return 'La sesión POS no está abierta o cambió. Solicite abrir la caja y vuelva a cotizar.';
    if (error.code?.startsWith('products.')) return 'Producto no disponible para el POS. Revise el carrito y vuelva a cotizar.';
    if (error.code === 'sale.total_changed') return 'El total cambió. Consulte una nueva cotización antes de cobrar.';
    if (error.status === 401) return 'Inicie sesión nuevamente para continuar.';
    if (error.status === 422 || error.status === 400) return 'La operación fue rechazada por datos inválidos. Revise cantidades, descuento y método; vuelva a cotizar.';
    return 'No se pudo obtener un resultado verificado. Revise la conexión y consulte nuevamente; no repita un cobro pendiente.';
  }
  async function run(action, work, readOnly = false) {
    if (state.busy) return false;
    patch({ busy: action, error: '', message: '', outcome: 'idle' });
    try { await work(); }
    catch (error) {
      patch({ error: failure(error, readOnly), outcome: !readOnly && useCases.pendingSale() ? 'uncertain'
        : [400, 409, 422].includes(error.status) || error.code?.startsWith('session.') || error.code?.startsWith('products.') ? 'rejected' : 'error' });
    } finally { patch({ busy: '', pending: useCases.pendingSale() }); }
    return true;
  }
  function result(value) {
    if (value.status === 'confirmed' && value.sale) {
      patch({ resultSale: value.sale, quote: null, reviewAccepted: false, outcome: 'confirmed',
        message: `Venta ${value.sale.reference} confirmada por Odoo (${value.sale.state}).` });
      onConfirmed();
    } else patch({ quote: null, reviewAccepted: false, resultSale: null, outcome: 'uncertain', message: uncertain });
  }
  function closeDetail() {
    detailGeneration += 1;
    patch({ detailOpen: false, detail: null, detailId: null, detailStatus: 'idle', detailError: '' });
  }
  async function loadHistory(request) {
    lastHistoryRequest = request;
    const generation = ++historyGeneration;
    closeDetail();
    patch({ historyStatus: 'loading', historyError: '' });
    try {
      const page = await useCases.salesHistoryPage({ search: request.query, beforeId: request.cursor, limit: 20 });
      if (generation !== historyGeneration) return false;
      if (!Array.isArray(page.items) || page.items.length > 20 || typeof page.hasMore !== 'boolean' ||
          (page.hasMore && (!Number.isInteger(page.nextBeforeId) || page.nextBeforeId <= 0 || (request.cursor > 0 && page.nextBeforeId >= request.cursor))))
        throw new Error('Invalid history page');
      patch({ history: page.items, historyLoaded: true, historyStatus: 'ready', historyQuery: request.query,
        historyCursors: request.cursors, historyIndex: request.index, historyHasMore: page.hasMore, historyNextBeforeId: page.nextBeforeId });
    } catch (error) {
      if (generation === historyGeneration) patch({ historyStatus: 'error', historyError: error.status === 401
        ? 'Inicie sesión nuevamente para consultar las órdenes.' : 'No se pudieron consultar las órdenes. Revise la conexión y vuelva a intentar.' });
    }
    return true;
  }
  const presenter = {
    read: () => state,
    subscribe(fn) { listeners.add(fn); return () => listeners.delete(fn); },
    setSale(value) {
      sale = value;
      const key = saleKey(value);
      if (key !== state.key) patch({ key, quote: null, method: '', reviewAccepted: false,
        notice: state.quote ? 'La cotización anterior dejó de estar vigente al cambiar la venta. Consulte el total nuevamente.' : state.notice });
    },
    selectMethod(value) { if (!state.busy && state.quote?.methods.some(method => String(method.id) === String(value))) patch({ method: value }); },
    acceptReview(value) { if (!state.busy && state.quote) patch({ reviewAccepted: value === true }); },
    quote() {
      if (state.busy || state.pending || !sale?.lines.length) return Promise.resolve(false);
      const requested = sale, key = state.key;
      patch({ quote: null, method: '', reviewAccepted: false });
      return run('Consultando cotización…', async () => {
        const session = await useCases.activeSession();
        const quote = await useCases.quoteSale({ sessionId: session.id, ...requested });
        if (key === state.key) patch({ quote, method: String(quote.methods[0]?.id ?? ''), notice: '' });
      });
    },
    confirm() {
      if (state.busy || state.pending || !state.quote || !state.method || !state.reviewAccepted) return Promise.resolve(false);
      const quote = state.quote, requested = sale, method = Number(state.method);
      patch({ detail: null, resultSale: null });
      return run('Registrando venta y efectivo…', async () => {
        patch({ quote: null, reviewAccepted: false });
        result(await useCases.confirmSale({ sessionId: quote.sessionId, ...requested }, method, quote.total));
      });
    },
    reconcile: () => run('Consultando operación…', async () => { patch({ detail: null, resultSale: null }); result(await useCases.reconcileSale()); }),
    editHistorySearch(value) {
      try { parseHistorySearch(value); patch({ historyTerm: value, historySearchError: '' }); }
      catch (error) { patch({ historySearchError: error.message }); }
    },
    searchHistory() {
      if (state.historySearchError) return Promise.resolve(false);
      return loadHistory({ cursor: 0, query: parseHistorySearch(state.historyTerm), cursors: [0], index: 0 });
    },
    history(before = 0) { return loadHistory({ cursor: before, query: state.historyQuery, cursors: [before], index: 0 }); },
    retryHistory: () => loadHistory(lastHistoryRequest),
    nextHistory() {
      if (state.historyStatus !== 'ready' || !state.historyHasMore) return Promise.resolve(false);
      const cursor = state.historyNextBeforeId;
      return loadHistory({ cursor, query: state.historyQuery, cursors: [...state.historyCursors.slice(0, state.historyIndex + 1), cursor], index: state.historyIndex + 1 });
    },
    previousHistory() {
      if (state.historyStatus !== 'ready' || state.historyIndex === 0) return Promise.resolve(false);
      const index = state.historyIndex - 1;
      return loadHistory({ cursor: state.historyCursors[index], query: state.historyQuery, cursors: state.historyCursors, index });
    },
    async detail(id) {
      if (!Number.isInteger(id) || id <= 0) return false;
      const generation = ++detailGeneration;
      patch({ detailOpen: true, detailId: id, detail: null, detailStatus: 'loading', detailError: '' });
      try {
        const detail = await useCases.saleDetail(id);
        if (!state.detailOpen || generation !== detailGeneration) return false;
        if (detail.id !== id) throw new Error('Unexpected sale identifier');
        patch({ detail, detailStatus: 'ready' });
      } catch {
        if (state.detailOpen && generation === detailGeneration)
          patch({ detailStatus: 'error', detailError: 'No se pudo consultar el detalle. Cierre o vuelva a intentar.' });
      }
      return true;
    },
    closeDetail,
    enterView(view) {
      if (view === activeView) return Promise.resolve(false);
      activeView = view;
      if (view !== 'historial' && state.detailOpen) closeDetail();
      if (view === 'historial') return presenter.history();
      if (view === 'cobro' && !state.quote && !state.pending) return presenter.quote();
      return Promise.resolve(false);
    },
  };
  return presenter;
}
