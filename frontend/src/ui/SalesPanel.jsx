import { useEffect, useRef, useState } from 'react';

export default function SalesPanel({ useCases, cart, discountCode, onLockChange, onConfirmed }) {
  const [quote, setQuote] = useState(null);
  const [method, setMethod] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [pending, setPending] = useState(() => useCases.pendingSale());
  const [history, setHistory] = useState([]);
  const [detail, setDetail] = useState(null);
  const locked = useRef(false);
  useEffect(() => { onLockChange(busy || !!pending); }, [busy, pending, onLockChange]);
  useEffect(() => { setQuote(null); }, [cart, discountCode]);
  const run = async (action) => {
    if (locked.current) return;
    locked.current = true; setBusy(true); setMessage('');
    try { await action(); } catch (e) { setMessage(e.message); }
    finally { setPending(useCases.pendingSale()); locked.current = false; setBusy(false); }
  };
  const showResult = (result) => {
    if (result.status === 'confirmed') {
      setMessage(`Venta ${result.sale.reference} confirmada por Odoo (${result.sale.state}). Total ${result.sale.total} ${result.sale.currency}.`);
      setDetail(result.sale); setQuote(null); onConfirmed();
    } else setMessage('Resultado incierto. No vuelva a cobrar: consulte el estado de esta operación.');
  };
  return <section className="card">
    <h2>Cobro e historial</h2>
    {pending && <p role="status">Operación pendiente: {pending.operationId}. Conserve este identificador.</p>}
    <button disabled={busy || !!pending || cart.isEmpty} onClick={() => run(async () => {
      const session = await useCases.activeSession();
      const result = await useCases.quoteSale({ sessionId: session.id, lines: cart.toRequestLines(), discountCode });
      setQuote(result); setMethod(String(result.methods[0]?.id ?? ''));
    })}>Consultar total con impuestos</button>
    {quote && <div>
      <p>Total a cobrar: <strong>{quote.total} {quote.currency}</strong> (efectivo exacto).</p>
      <label>Método de pago <select disabled={busy || !!pending} value={method} onChange={e => setMethod(e.target.value)}>
        {quote.methods.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
      </select></label>
      <button disabled={busy || !!pending || !method} onClick={() => run(async () => showResult(await useCases.confirmSale(
        { sessionId: quote.sessionId, lines: cart.toRequestLines(), discountCode }, Number(method), quote.total
      )))}>Confirmar venta y efectivo</button>
    </div>}
    {pending && <button disabled={busy} onClick={() => run(async () => showResult(await useCases.reconcileSale()))}>Consultar operación pendiente</button>}
    <p role="status" aria-live="polite">{busy ? 'Consultando…' : message}</p>
    <button disabled={busy} onClick={() => run(async () => { setHistory(await useCases.salesHistory(0)); setDetail(null); })}>Ver historial</button>
    <ul>{history.map(s => <li key={s.id}><button disabled={busy} onClick={() => run(async () => setDetail(await useCases.saleDetail(s.id)))}>{s.reference} · {s.total} {s.currency} · {s.state}</button></li>)}</ul>
    {history.length === 20 && <button disabled={busy} onClick={() => run(async () => setHistory(await useCases.salesHistory(history.at(-1).id)))}>Página siguiente</button>}
    {detail && <article><h3>Detalle {detail.reference}</h3><p>Estado Odoo: {detail.state}; total: {detail.total} {detail.currency}; impuesto: {detail.tax}; pagado: {detail.paid}</p>
      <ul>{detail.lines.map((l,i) => <li key={i}>Producto {l.productId}: {l.quantity} × {l.unitPrice}; descuento {l.discountPercent}%; neto {l.net}; impuesto {l.tax}</li>)}</ul>
      <p>Métodos: {detail.methods.map(m => m.name).join(', ')}</p>
    </article>}
  </section>;
}
