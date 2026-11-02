import { useEffect, useMemo, useRef, useSyncExternalStore } from 'react';
import { createSalesPresenter, saleKey } from './salesPresenter.js';

function VerifiedDetail({ sale }) {
  return <article aria-label={`Detalle ${sale.reference}`}>
    <h3>Detalle {sale.reference}</h3>
    <p>Resultado consultado en Odoo · Estado: <strong>{sale.state}</strong></p>
    <dl className="totals"><dt>Importe registrado ({sale.currency})</dt><dd>{sale.total}</dd>
      <dt>Impuestos</dt><dd>{sale.tax}</dd><dt>Pagado</dt><dd>{sale.paid}</dd></dl>
    <ul>{sale.lines.map((line, i) => <li key={i}>Producto {line.productId}: {line.quantity} × {line.unitPrice}; descuento {line.discountPercent}%; neto {line.net}; impuesto {line.tax}</li>)}</ul>
    <p>Métodos registrados: {sale.methods.map(method => method.name).join(', ') || 'No informados'}</p>
  </article>;
}

export default function SalesPanel({ useCases, cart, discountCode, onLockChange, onConfirmed, view = 'venta' }) {
  const title = useRef(null);
  useEffect(() => { title.current?.focus(); }, [view]);
  const confirmed = useRef(onConfirmed);
  confirmed.current = onConfirmed;
  const presenter = useMemo(() => createSalesPresenter(useCases, () => confirmed.current()), [useCases]);
  const state = useSyncExternalStore(presenter.subscribe, presenter.read, presenter.read);
  const sale = { lines: cart.toRequestLines(), discountCode };
  const key = saleKey(sale);
  useEffect(() => { presenter.setSale({ lines: cart.toRequestLines(), discountCode }); }, [presenter, cart, discountCode]);
  useEffect(() => { onLockChange(!!state.busy || !!state.pending); }, [state.busy, state.pending, onLockChange]);
  const quote = state.key === key ? state.quote : null;
  const syncSale = action => { presenter.setSale(sale); return action(); };
  return <section className="card sales-panel" aria-labelledby="sales-title">
    <h2 id="sales-title" ref={title} tabIndex="-1">{{ venta: 'Cobro · efectivo exacto', historial: 'Historial de ventas', operaciones: 'Operaciones pendientes' }[view]}</h2>
    {view === 'venta' && <section aria-labelledby="checkout-title">{state.pending && <p role="status">Hay una operación pendiente. <a href="#operaciones">Consultar resultado antes de cobrar</a>.</p>}<h3 id="checkout-title">Revisión y pago en efectivo</h3>
      <button disabled={!!state.busy || !!state.pending || cart.isEmpty} onClick={() => syncSale(presenter.quote)}>Consultar total con impuestos</button>
      {!quote && <p className="muted">No hay cotización vigente. Consulte el importe antes de confirmar.</p>}
      {quote && <div>
        <p>Total verificado por API: <strong>{quote.total} {quote.currency}</strong>. Cobro únicamente en efectivo exacto, sin vuelto.</p>
        <div className="ticket" aria-label="Ticket de revisión">
          <h4>Revisión antes de confirmar</h4>
          <ul>{quote.lines.map(line => <li key={line.productId}><strong>Producto {line.productId}</strong><span>{line.quantity} × {line.unitPrice} · descuento {line.discountPercent}%</span><span>Neto {line.net} · impuesto {line.tax}</span></li>)}</ul>
          <p>Importe verificado: <strong>{quote.total} {quote.currency}</strong></p>
        </div>
        <label>Método configurado (efectivo)
          <select disabled={!!state.busy || !!state.pending} value={state.method} onChange={e => presenter.selectMethod(e.target.value)}>
            {quote.methods.map(method => <option key={method.id} value={method.id}>{method.name}</option>)}
          </select>
        </label>
        {!quote.methods.length && <p role="alert">No hay método de efectivo disponible. Revise la configuración de la sesión POS.</p>}
        <button disabled={!!state.busy || !!state.pending || !state.method} onClick={() => syncSale(presenter.confirm)}>Confirmar venta y efectivo</button>
      </div>}
    </section>}
    <p role="status" aria-live="polite">{state.busy || state.message}</p>
    {state.error && <p className="error" role="alert">{state.error}</p>}
    {view === 'operaciones' && <section id="operaciones" tabIndex="-1" aria-labelledby="pending-title"><h3 id="pending-title">Operaciones pendientes</h3>
      {state.pending ? <><p>Operación pendiente: <span className="operation-id">{state.pending.operationId}</span>. No vuelva a cobrar.</p>
        <button disabled={!!state.busy} onClick={presenter.reconcile}>Consultar operación pendiente</button></> : <p>No hay operaciones pendientes para este operador.</p>}
    </section>}
    {view === 'historial' && <section id="historial" tabIndex="-1" aria-labelledby="history-title"><h3 id="history-title">Historial</h3>
      <button disabled={!!state.busy} onClick={() => presenter.history()}>Ver historial</button>
      {state.historyLoaded && !state.history.length && <p>No hay ventas en esta página.</p>}
      <ul className="sales-history">{state.history.map(sale => <li key={sale.id}>
        <p><strong>{sale.reference}</strong> · {sale.total} {sale.currency} · Estado Odoo: {sale.state}</p>
        <button disabled={!!state.busy} onClick={() => presenter.detail(sale.id)}>Ver detalle de {sale.reference}</button>
      </li>)}</ul>
      {state.history.length === 20 && <button disabled={!!state.busy} onClick={() => presenter.history(state.history.at(-1).id)}>Página siguiente</button>}
      {state.detail && <VerifiedDetail sale={state.detail} />}
    </section>}
    {view !== 'historial' && state.resultSale && <VerifiedDetail sale={state.resultSale} />}
  </section>;
}
