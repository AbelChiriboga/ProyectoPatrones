import SafeDialog from './SafeDialog.jsx';
import { displayAmount, displayDateUtc, orderStateLabel, quoteSummary } from './salesViewModel.js';

export function OrderState({ value }) {
  return <span className={`order-state state-${['paid', 'done', 'draft', 'cancel'].includes(value) ? value : 'unknown'}`}>
    {orderStateLabel(value)} <small>({value})</small>
  </span>;
}
export function VerifiedDetail({ sale, showHeading = true }) {
  return <article className="verified-detail" aria-label={`Detalle ${sale.reference}`}>
    <div className="panel-heading"><div><p className="eyebrow">Información registrada en Odoo</p>{showHeading ? <h2>Detalle {sale.reference}</h2> : <strong>{sale.reference}</strong>}</div><OrderState value={sale.state} /></div>
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
export function SaleDetailDialog({ state, onClose, onRetry }) {
  if (!state.detailOpen) return null;
  return <SafeDialog title={state.detail ? `Detalle ${state.detail.reference}` : `Detalle de venta ${state.detailId}`}
    description="Consulta de una orden registrada en Odoo. Cerrar no altera la venta ni el historial." onDismiss={onClose}>
    <div className="dialog-body">
      {state.detailStatus === 'loading' && <p role="status">Consultando detalle…</p>}
      {state.detailStatus === 'error' && <div className="notice notice-error" role="alert"><p>{state.detailError}</p><button className="secondary" onClick={onRetry}>Reintentar detalle</button></div>}
      {state.detailStatus === 'ready' && state.detail && <VerifiedDetail sale={state.detail} showHeading={false} />}
    </div>
    <footer className="dialog-actions"><button type="button" className="secondary" data-dialog-initial-focus onClick={onClose}>Cerrar detalle</button></footer>
  </SafeDialog>;
}
export function HistoryView({ state, onRetry, onRefresh, onDetail, onNext, onPrevious, onSearchTerm, onCloseDetail, onRetryDetail }) {
  const loading = ['idle', 'loading'].includes(state.historyStatus);
  return <>
    <section className="card history-panel" aria-label="Órdenes registradas">
      <div className="history-search">
        <div><label htmlFor="history-search">Buscar órdenes</label>
          <input id="history-search" type="search" placeholder="Referencia de la orden o referencia POS" value={state.historyTerm ?? ''}
            aria-invalid={!!state.historySearchError} aria-describedby={`history-search-help${state.historySearchError ? ' history-search-error' : ''}`}
            onChange={event => onSearchTerm(event.target.value)} />
          <p id="history-search-help" className="hint">Resultados automáticos al escribir. Hasta 100 caracteres de la referencia.</p>
          {state.historySearchError && <p id="history-search-error" role="alert" className="error">{state.historySearchError}</p>}
        </div>
      </div>
      <div className="panel-heading"><p className="hint">{state.historyQuery ? `Resultados para «${state.historyQuery}»` : 'Ventas del punto de venta autorizado'} · Registro más reciente primero</p>
        {state.historyStatus === 'ready' && <button className="secondary" onClick={onRefresh}>Actualizar órdenes</button>}</div>
      {loading && <p className="empty-state" role="status">Cargando órdenes…</p>}
      {state.historyStatus === 'error' && <div className="notice notice-error" role="alert"><strong>No se pudo cargar el historial</strong><p>{state.historyError}</p><button className="secondary" onClick={onRetry}>Reintentar historial</button></div>}
      {(state.historyStatus === 'ready' || state.historyLoaded) && <>
        {!state.history.length ? (state.historyStatus === 'ready' && <div className="empty-state"><strong>No hay ventas en esta página</strong><p>{state.historyQuery ? 'No se encontraron órdenes para la referencia consultada.' : 'Las órdenes aparecerán aquí cuando estén registradas en Odoo.'}</p></div>)
          : <div className="history-table-wrap" aria-busy={loading}><table className="history-table"><caption className="sr-only">Ventas reales de Odoo, página {(state.historyIndex ?? 0) + 1}</caption>
            <thead><tr><th scope="col">Referencia</th><th scope="col">Fecha (UTC)</th><th scope="col">Total</th><th scope="col">Estado</th><th scope="col">Detalle</th></tr></thead>
            <tbody>{state.history.map(sale => <tr key={sale.id}><th scope="row">{sale.reference}</th><td>{displayDateUtc(sale.dateUtc)}</td>
              <td className="price">{displayAmount(sale.total, sale.currency)}</td><td><OrderState value={sale.state} /></td>
              <td><button type="button" className="secondary" disabled={loading} onClick={() => onDetail(sale.id)}>Ver detalle de {sale.reference}</button></td></tr>)}</tbody>
          </table></div>}
      </>}
      <nav className="history-pagination" aria-label="Páginas del historial">
        <button type="button" className="secondary" disabled={loading || state.historyStatus !== 'ready' || !(state.historyIndex > 0)} onClick={onPrevious}>Anterior</button>
        <span role="status">Página {(state.historyIndex ?? 0) + 1}</span>
        <button type="button" className="secondary" disabled={loading || state.historyStatus !== 'ready' || !state.historyHasMore} onClick={onNext}>Siguiente</button>
      </nav>
    </section>
    <SaleDetailDialog state={state} onClose={onCloseDetail} onRetry={onRetryDetail} />
  </>;
}
