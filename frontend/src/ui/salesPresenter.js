// Observable UI state; use cases own API contracts, persistence and idempotency.
export const saleKey = sale => JSON.stringify(sale);
export function createSalesPresenter(useCases, onConfirmed = () => {}) {
  let sale = null, activeView = null, historyRequest = null;
  let state = { key: '', quote: null, method: '', reviewAccepted: false, busy: '', error: '', message: '', notice: '', outcome: 'idle',
    pending: useCases.pendingSale(), history: [], historyLoaded: false, historyStatus: 'idle', historyError: '', detail: null, resultSale: null };
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
      patch({ detail: value.sale, resultSale: value.sale, quote: null, reviewAccepted: false, outcome: 'confirmed',
        message: `Venta ${value.sale.reference} confirmada por Odoo (${value.sale.state}).` });
      onConfirmed();
    } else patch({ quote: null, reviewAccepted: false, resultSale: null, outcome: 'uncertain', message: uncertain });
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
    history(before = 0) {
      if (historyRequest) return historyRequest;
      patch({ historyStatus: 'loading', historyError: '', detail: null });
      historyRequest = Promise.resolve().then(async () => {
        try { patch({ history: await useCases.salesHistory(before), historyLoaded: true, historyStatus: 'ready' }); }
        catch (error) { patch({ historyStatus: 'error', historyError: error.status === 401 ? 'Inicie sesión nuevamente para consultar las órdenes.' : 'No se pudieron consultar las órdenes. Revise la conexión y vuelva a intentar.' }); }
        finally { historyRequest = null; }
      });
      return historyRequest;
    },
    detail: id => run('Consultando detalle…', async () => { patch({ detail: null }); patch({ detail: await useCases.saleDetail(id) }); }, true),
    enterView(view) {
      if (view === activeView) return Promise.resolve(false);
      activeView = view;
      if (view === 'historial') return presenter.history();
      if (view === 'cobro' && !state.quote && !state.pending) return presenter.quote();
      return Promise.resolve(false);
    },
  };
  return presenter;
}
