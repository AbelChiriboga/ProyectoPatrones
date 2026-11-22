import { useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { createSalesPresenter, saleKey } from './salesPresenter.js';
import { HistoryView, QuoteReview, VerifiedDetail } from './SalesViews.jsx';
import { displayAmount } from './salesViewModel.js';

function OperationMessage({ state }) {
  if (!state.busy && !state.message && !state.error) return null;
  const title = { confirmed: 'Operación confirmada', uncertain: 'Resultado incierto', rejected: 'Operación rechazada', error: 'Consulta no disponible', idle: 'Procesando' }[state.outcome];
  return <div className={`notice notice-${state.busy ? 'info' : state.outcome}`} role={state.error ? 'alert' : 'status'} aria-live="polite">
    <strong>{state.busy ? state.busy : title}</strong>
    {!state.busy && <p>{state.error || state.message}</p>}
    {!state.busy && state.outcome === 'uncertain' && <a href="#operaciones">Consultar la operación pendiente</a>}
    {!state.busy && state.resultSale && <p className="result-amount">{state.resultSale.reference} · {displayAmount(state.resultSale.total, state.resultSale.currency)}</p>}
  </div>;
}

export default function SalesPanel({ useCases, cart, discountCode, onLockChange, onConfirmed, onContinue, view = 'venta', presenter: externalPresenter }) {
  const confirmed = useRef(onConfirmed);
  confirmed.current = onConfirmed;
  const internalPresenter = useMemo(() => createSalesPresenter(useCases, () => confirmed.current()), [useCases]);
  const presenter = externalPresenter ?? internalPresenter;
  useEffect(() => () => presenter.dispose?.(), [presenter]);
  const state = useSyncExternalStore(presenter.subscribe, presenter.read, presenter.read);
  const sale = { lines: cart.toRequestLines(), discountCode };
  const key = saleKey(sale);
  useEffect(() => { presenter.setSale({ lines: cart.toRequestLines(), discountCode }); }, [presenter, cart, discountCode]);
  useEffect(() => { presenter.enterView(view); }, [presenter, view]);
  useEffect(() => { onLockChange(!!state.busy || !!state.pending); }, [state.busy, state.pending, onLockChange]);
  const quote = state.key === key ? state.quote : null;
  const syncSale = action => { presenter.setSale(sale); return action(); };

  // Cálculo reactivo de efectivo recibido y vuelto en tiempo real (tipo Odoo POS)
  const [tenderedAmount, setTenderedAmount] = useState('');
  useEffect(() => {
    if (quote?.total != null) {
      setTenderedAmount(quote.total.toFixed(2));
    }
  }, [quote?.total]);

  const tenderedNum = parseFloat(tenderedAmount) || 0;
  const totalDue = quote?.total ?? 0;
  const changeDue = Math.round((tenderedNum - totalDue) * 100) / 100;
  const isTenderedSufficient = tenderedNum >= totalDue && totalDue > 0;

  // Detección de conectividad para cola resiliente ante caídas temporales de Odoo
  const [isOnline, setIsOnline] = useState(typeof navigator !== 'undefined' ? navigator.onLine : true);
  useEffect(() => {
    const handleOnline = () => {
      setIsOnline(true);
      if (state.pending) presenter.reconcile();
    };
    const handleOffline = () => setIsOnline(false);
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [state.pending, presenter]);

  const handlePaymentKeyDown = (e) => {
    if (e.key === 'Enter') {
      if (!state.busy && !state.pending && state.method && state.reviewAccepted && isTenderedSufficient) {
        e.preventDefault();
        syncSale(presenter.confirm);
      }
    }
  };

  if (view === 'venta') return <div className="sale-next">
    <OperationMessage state={state} />
    {state.pending && <div className="notice notice-uncertain" role="status">Hay una operación pendiente. <a href="#operaciones">Consulta su resultado antes de cobrar otra vez.</a></div>}
    {state.notice && <p className="notice notice-warning" role="status">{state.notice}</p>}
    <div className="action-bar"><div><strong>Siguiente paso: revisar y cobrar</strong><p className="hint">Consulta el total final y confirma únicamente el efectivo exacto.</p></div>
      <button type="button" disabled={!!state.busy || !!state.pending || cart.isEmpty} onClick={onContinue}>Revisar y continuar a cobro <span aria-hidden="true">→</span></button></div>
  </div>;

  return <div className="sales-content">
    <OperationMessage state={state} />
    {!isOnline && <div className="notice notice-warning" role="status">Modo sin conexión. La sincronización se reanudará automáticamente al recuperar red.</div>}
    {view === 'cobro' && <>
      <div className="checkout-toolbar"><a href="#venta" className="back-link">← Volver a venta</a>
        <button type="button" className="secondary" disabled={!!state.busy || !!state.pending || cart.isEmpty} onClick={() => syncSale(presenter.quote)}>Consultar total con impuestos</button></div>
      {state.pending && <div className="notice notice-uncertain" role="status">Hay un cobro con resultado pendiente. <a href="#operaciones">Consultar antes de realizar otra operación</a>.</div>}
      {state.notice && <p className="notice notice-warning" role="status">{state.notice}</p>}
      {!quote && !state.busy && <div className="card empty-state"><strong>{cart.isEmpty ? 'No hay productos para cotizar' : 'No hay cotización vigente'}</strong>
        <p>{cart.isEmpty ? 'Regresa a Venta para preparar el carrito.' : 'Consulta el total con impuestos antes de confirmar. Una cotización anterior no autoriza un nuevo cobro.'}</p></div>}
      {quote && <div className="checkout-columns">
        <QuoteReview quote={quote} cart={cart} />
        <section className="card payment-panel" aria-labelledby="payment-title"><p className="eyebrow">Paso final</p><h2 id="payment-title">Efectivo exacto</h2>
          <p className="hint">Solo se admite efectivo por el importe verificado. No se registra vuelto.</p>
          <label htmlFor="cash-method">Método de pago configurado</label>
          <select id="cash-method" disabled={!!state.busy || !!state.pending} value={state.method} onChange={e => presenter.selectMethod(e.target.value)}>
            {quote.methods.map(method => <option key={method.id} value={method.id}>{method.name} (importe exacto)</option>)}
          </select>
          {!quote.methods.length && <p className="error" role="alert">No hay método de efectivo disponible. Revise la sesión POS.</p>}
          <div className="payment-total"><span>Importe a registrar</span><strong>{displayAmount(quote.total, quote.currency)}</strong></div>

          {/* Desglose reactivo de efectivo y vuelto en tiempo real */}
          <div className="tendered-calculator">
            <label htmlFor="tendered-amount">Efectivo recibido del cliente ({quote.currency})</label>
            <div className="tendered-input-row">
              <input id="tendered-amount" type="number" step="0.01" min="0"
                disabled={!!state.busy || !!state.pending}
                value={tenderedAmount}
                onChange={e => setTenderedAmount(e.target.value)}
                onKeyDown={handlePaymentKeyDown} />
              <button type="button" className="secondary quick-cash-btn" title="Pagar monto exacto"
                onClick={() => setTenderedAmount(quote.total.toFixed(2))}>Exacto</button>
            </div>
            <div className="quick-denominations">
              {[5, 10, 20, 50, 100].filter(d => d >= quote.total || d === 10 || d === 20).map(amount => (
                <button key={amount} type="button" className="secondary bill-btn"
                  disabled={!!state.busy || !!state.pending}
                  onClick={() => setTenderedAmount(amount.toFixed(2))}>${amount}</button>
              ))}
            </div>
            <div className="change-indicator" aria-live="polite">
              {changeDue >= 0 ? (
                <div className="change-positive">
                  <span>Vuelto a entregar:</span>
                  <strong>{displayAmount(changeDue, quote.currency)}</strong>
                </div>
              ) : (
                <div className="change-negative" role="alert">
                  <span>Monto insuficiente. Faltan:</span>
                  <strong>{displayAmount(Math.abs(changeDue), quote.currency)}</strong>
                </div>
              )}
            </div>
          </div>

          <label className="check-label"><input type="checkbox" checked={state.reviewAccepted} disabled={!!state.busy || !!state.pending}
            onChange={e => presenter.acceptReview(e.target.checked)} /><span>He revisado los productos y el importe de efectivo exacto.</span></label>
          <button type="button" className="confirm-button" disabled={!!state.busy || !!state.pending || !state.method || !state.reviewAccepted || !isTenderedSufficient}
            onClick={() => syncSale(presenter.confirm)}>Confirmar venta y efectivo</button>
          <p className="hint">La venta solo se mostrará confirmada después de verificar el resultado en Odoo.</p>
        </section>
      </div>}
      {state.resultSale && <div className="card"><VerifiedDetail sale={state.resultSale} /></div>}
    </>}
    {view === 'historial' && <HistoryView state={state} onRetry={presenter.retryHistory} onRefresh={() => presenter.history()} onDetail={presenter.detail}
      onNext={presenter.nextHistory} onPrevious={presenter.previousHistory} onSearchTerm={presenter.editHistorySearch}
      onCloseDetail={presenter.closeDetail} onRetryDetail={() => presenter.detail(state.detailId)} />}
    {view === 'operaciones' && <section className="card pending-panel" aria-label="Consulta de resultados inciertos">
      <p className="eyebrow">Consulta y conciliación</p>
      {state.pending ? <><h2>Una operación necesita verificación</h2><p>Conserva este identificador. No repitas el cobro ni generes una venta nueva para resolverlo.</p>
        <p className="operation-id">{state.pending.operationId}</p>
        <button type="button" disabled={!!state.busy} onClick={presenter.reconcile}>Consultar operación pendiente</button></>
        : <div className="empty-state"><h2>No hay operaciones pendientes</h2><p>No hay resultados inciertos guardados para este operador.</p><a href="#venta">Volver a venta</a></div>}
      {state.resultSale && <VerifiedDetail sale={state.resultSale} />}
    </section>}
  </div>;
}
