import { displayAmount, orderStateLabel, quoteSummary } from './salesViewModel.js';

export function OrderState({ value }) {
  return <span className={`order-state state-${['paid', 'done', 'draft', 'cancel'].includes(value) ? value : 'unknown'}`}>
    {orderStateLabel(value)} <small>({value})</small>
  </span>;
}
export function VerifiedDetail({ sale }) {
  return <article className="verified-detail" aria-label={`Detalle ${sale.reference}`}>
    <div className="panel-heading"><div><p className="eyebrow">Información registrada en Odoo</p><h2>Detalle {sale.reference}</h2></div><OrderState value={sale.state} /></div>
    <dl className="totals"><dt>Importe registrado</dt><dd className="total">{displayAmount(sale.total, sale.currency)}</dd>
      <dt>Impuestos registrados</dt><dd>{displayAmount(sale.tax, sale.currency)}</dd><dt>Pagado</dt><dd>{displayAmount(sale.paid, sale.currency)}</dd></dl>
    <ul className="review-lines">{sale.lines.map((line, i) => <li key={i}><strong>Producto {line.productId} · {line.quantity} unidades</strong>
      <span>{displayAmount(line.unitPrice, sale.currency)} c/u · descuento {line.discountPercent}%</span>
      <span>Neto {displayAmount(line.net, sale.currency)} · impuestos {displayAmount(line.tax, sale.currency)}</span></li>)}</ul>
    <p className="hint">Métodos registrados: {sale.methods.map(method => method.name).join(', ') || 'No informados'}</p>
  </article>;
}
export function QuoteReview({ quote, cart }) {
  const totals = quoteSummary(quote);
  return <section className="card review-panel" aria-labelledby="review-title">
    <p className="eyebrow">Cotización vigente</p><h2 id="review-title">Revisión de la venta</h2>
    <ul className="review-lines">{quote.lines.map(line => <li key={line.productId}>
      <div className="review-line-heading"><strong>{cart.lines.find(item => item.productId === line.productId)?.name ?? `Producto ${line.productId}`}</strong><span>{line.quantity} unidades</span></div>
      <span>{displayAmount(line.unitPrice, quote.currency)} c/u · descuento {line.discountPercent}%</span>
      <span>Neto {displayAmount(line.net, quote.currency)} · impuestos {displayAmount(line.tax, quote.currency)}</span>
    </li>)}</ul>
    <dl className="totals"><dt>Subtotal de líneas</dt><dd>{displayAmount(totals.subtotal, quote.currency)}</dd>
      <dt>Descuento aplicado</dt><dd>{displayAmount(totals.discount, quote.currency)}</dd>
      <dt>Impuestos de la cotización</dt><dd>{displayAmount(totals.tax, quote.currency)}</dd>
      <dt>Total verificado por API</dt><dd className="total">{displayAmount(totals.total, quote.currency)}</dd></dl>
    <p className="hint">Los importes proceden de la cotización real. Cualquier cambio requiere una nueva consulta.</p>
  </section>;
}
export function HistoryView({ state, onRetry, onDetail, onNext }) {
  const loading = ['idle', 'loading'].includes(state.historyStatus);
  return <section className="card history-panel" aria-label="Órdenes registradas">
    <div className="panel-heading"><p className="hint">Ventas del punto de venta autorizado</p>
      {state.historyStatus === 'ready' && <button className="secondary" disabled={!!state.busy} onClick={onRetry}>Actualizar órdenes</button>}</div>
    {loading && <p className="empty-state" role="status">Cargando órdenes…</p>}
    {state.historyStatus === 'error' && <div className="notice notice-error" role="alert"><strong>No se pudo cargar el historial</strong><p>{state.historyError}</p><button className="secondary" onClick={onRetry}>Reintentar historial</button></div>}
    {state.historyStatus === 'ready' && <>
      {!state.history.length && <div className="empty-state"><strong>No hay ventas en esta página</strong><p>Las órdenes aparecerán aquí cuando estén registradas en Odoo.</p></div>}
      <ul className="sales-history">{state.history.map(sale => <li key={sale.id}>
        <div><strong>{sale.reference}</strong><OrderState value={sale.state} /></div>
        <span className="price">{displayAmount(sale.total, sale.currency)}</span>
        <button className="secondary" disabled={!!state.busy} onClick={() => onDetail(sale.id)}>Ver detalle de {sale.reference}</button>
      </li>)}</ul>
      {state.history.length === 20 && <button className="secondary" disabled={!!state.busy} onClick={onNext}>Página siguiente</button>}
    </>}
    {state.detail && <VerifiedDetail sale={state.detail} />}
  </section>;
}
