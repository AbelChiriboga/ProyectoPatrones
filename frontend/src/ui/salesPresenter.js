// Observable presentation state. No React, storage, HTTP or fiscal calculation here.
export const saleKey = sale => JSON.stringify(sale);
export function createSalesPresenter(useCases, onConfirmed = () => {}) {
  let sale = null;
  let state = { key: '', quote: null, method: '', busy: '', error: '', message: '',
    pending: useCases.pendingSale(), history: [], historyLoaded: false, detail: null };
  const listeners = new Set();
  const patch = changes => { state = { ...state, ...changes }; listeners.forEach(fn => fn()); };
  const uncertain = 'Resultado incierto. No vuelva a cobrar; consulte la operación pendiente con el mismo identificador.';
  function failure(error) {
    if (useCases.pendingSale()) return uncertain;
    if (error.code?.startsWith('session.')) return 'La sesión POS no está abierta o cambió. Solicite abrir la caja y vuelva a cotizar.';
    if (error.code?.startsWith('products.')) return 'Producto no disponible para el POS. Revise el carrito y vuelva a cotizar.';
    if (error.code === 'sale.total_changed') return 'El total cambió. Consulte una nueva cotización antes de cobrar.';
    if (error.status === 401) return 'Inicie sesión nuevamente para continuar.';
    if (error.status === 422 || error.status === 400) return 'La operación fue rechazada por datos inválidos. Revise cantidades, descuento y método; vuelva a cotizar.';
    return 'No se pudo obtener un resultado verificado. Revise la conexión y consulte nuevamente; no repita un cobro pendiente.';
  }
  async function run(action, work) {
    if (state.busy) return false;
    patch({ busy: action, error: '', message: '' });
    try { await work(); }
    catch (error) { patch({ error: failure(error) }); }
    finally { patch({ busy: '', pending: useCases.pendingSale() }); }
    return true;
  }
  function result(value) {
    if (value.status === 'confirmed' && value.sale) {
      patch({ detail: value.sale, quote: null, message: `Venta ${value.sale.reference} confirmada por Odoo (${value.sale.state}).` });
      onConfirmed();
    } else patch({ quote: null, message: uncertain });
  }
  return {
    read: () => state,
    subscribe(fn) { listeners.add(fn); return () => listeners.delete(fn); },
    setSale(value) {
      sale = value;
      const key = saleKey(value);
      if (key !== state.key) patch({ key, quote: null, method: '' });
    },
    selectMethod(value) { if (!state.busy) patch({ method: value }); },
    quote() {
      if (state.busy || state.pending || !sale?.lines.length) return Promise.resolve(false);
      const requested = sale, key = state.key;
      patch({ quote: null, method: '' });
      return run('Cotizando…', async () => {
        const session = await useCases.activeSession();
        const quote = await useCases.quoteSale({ sessionId: session.id, ...requested });
        if (key === state.key) patch({ quote, method: String(quote.methods[0]?.id ?? '') });
      });
    },
    confirm() {
      if (state.busy || state.pending || !state.quote || !state.method) return Promise.resolve(false);
      const quote = state.quote, requested = sale, method = Number(state.method);
      patch({ detail: null });
      return run('Confirmando venta…', async () => {
        // Consume quote before awaiting: failures require an explicit new quote.
        patch({ quote: null });
        result(await useCases.confirmSale({ sessionId: quote.sessionId, ...requested }, method, quote.total));
      });
    },
    reconcile: () => run('Conciliando operación…', async () => { patch({ detail: null }); result(await useCases.reconcileSale()); }),
    history: (before = 0) => run('Consultando historial…', async () => patch({ history: await useCases.salesHistory(before), historyLoaded: true, detail: null })),
    detail: id => run('Consultando detalle…', async () => { patch({ detail: null }); patch({ detail: await useCases.saleDetail(id) }); }),
  };
}
