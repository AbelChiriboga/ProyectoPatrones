import { useEffect, useState, useCallback, useMemo, useRef, useSyncExternalStore } from 'react';
import { Cart, lineSubtotal } from '../domain/cart.js';
import SalesPanel from './SalesPanel.jsx';
import QuantityInput from './QuantityInput.jsx';
import CatalogPanel from './CatalogPanel.jsx';
import { createCatalogPresenter } from './catalogPresenter.js';
import { createViewNavigation, browserHashPort, viewFromHash } from './viewNavigation.js';
import { displayAmount } from './salesViewModel.js';

export default function PosPage({ useCases, username, onLogout, initialView = 'venta' }) {
  const navigation = useMemo(() => createViewNavigation(browserHashPort), []);
  const view = useSyncExternalStore(navigation.subscribe, navigation.read, () => viewFromHash(initialView));
  const heading = useRef(null);
  useEffect(() => { heading.current?.focus(); }, [view]);
  const catalog = useMemo(() => createCatalogPresenter(useCases), [useCases]);
  const catalogState = useSyncExternalStore(catalog.subscribe, catalog.read, catalog.read);
  useEffect(() => { catalog.load(); return catalog.dispose; }, [catalog]);
  const [checkoutLocked, setCheckoutLocked] = useState(false);
  const [cart, setCart] = useState(Cart.empty());
  const [cartMessage, setCartMessage] = useState('');
  const [discounts, setDiscounts] = useState([]);
  const [discountError, setDiscountError] = useState('');
  const [discountCode, setDiscountCode] = useState('none');
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState('');
  const [previewLoading, setPreviewLoading] = useState(false);
  const cartKey = JSON.stringify({ lines: cart.toRequestLines(), discountCode });
  const currentPreview = preview?.key === cartKey ? preview.value : null;

  useEffect(() => {
    let cancelled = false;
    useCases.listDiscounts().then(result => { if (!cancelled) { setDiscounts(result); setDiscountError(''); } })
      .catch(() => { if (!cancelled) setDiscountError('No se pudieron consultar los descuentos. Vuelva a intentar más tarde.'); });
    return () => { cancelled = true; };
  }, [useCases]);
  useEffect(() => {
    let cancelled = false;
    setPreviewLoading(true);
    useCases.previewSale(cart, discountCode)
      .then(result => { if (!cancelled) { setPreview({ key: cartKey, value: result }); setPreviewError(''); } })
      .catch(() => { if (!cancelled) { setPreview(null); setPreviewError('No se pudo verificar el total provisional. Consulte nuevamente antes de cobrar.'); } })
      .finally(() => { if (!cancelled) setPreviewLoading(false); });
    return () => { cancelled = true; };
  }, [useCases, cart, discountCode, cartKey]);

  const updateCart = operation => {
    try { setCart(operation()); setCartMessage(''); } catch (error) { setCartMessage(error.message); }
  };
  const commitQuantity = useCallback((id, quantity) => {
    const current = cart.lines.find(line => line.productId === id);
    if (current.quantity === quantity) return;
    setCart(useCases.changeQuantity(cart, id, quantity));
    setCartMessage('');
  }, [cart, useCases]);
  const quantityCallbacks = useMemo(() => new Map(cart.lines.map(line =>
    [line.productId, quantity => commitQuantity(line.productId, quantity)])), [cart, commitQuantity]);
  const title = { venta: 'Prepara una venta', cobro: 'Revisión y pago', historial: 'Historial de ventas', operaciones: 'Operaciones pendientes' }[view];

  return <div className="pos-app">
    <header className="topbar">
      <div className="brand"><span className="brand-mark" aria-hidden="true">P</span><div><strong>POS Odoo</strong><span>Punto de venta</span></div></div>
      <div className="operator"><span>Operador: <strong>{username}</strong></span><button type="button" className="link" onClick={onLogout}>Cerrar sesión</button></div>
    </header>
    <nav className="workspace-nav" aria-label="Áreas del POS">
      <ol className="steps"><li><a href="#venta" aria-current={view === 'venta' ? 'step' : undefined}><span className="step-number">1</span> Venta</a></li>
        <li className="step-arrow" aria-hidden="true">→</li><li><a href="#cobro" aria-current={view === 'cobro' ? 'step' : undefined}><span className="step-number">2</span> Cobro</a></li></ol>
      <div className="secondary-nav"><a href="#historial" aria-current={view === 'historial' ? 'page' : undefined}>Historial</a><a href="#operaciones" aria-current={view === 'operaciones' ? 'page' : undefined}>Operaciones pendientes</a></div>
    </nav>
    <main className="workspace" id="main-content">
      <div className="view-heading"><div><p className="eyebrow">{view === 'venta' ? 'Paso 1 de 2' : view === 'cobro' ? 'Paso 2 de 2' : 'Consulta del POS'}</p><h1 ref={heading} tabIndex="-1">{title}</h1></div>
        <p>{view === 'venta' ? 'Elige productos, revisa cantidades y continúa al cobro.' : view === 'cobro' ? 'Verifica la cotización antes de confirmar el efectivo exacto.' : 'Consulta información registrada; no crea nuevas ventas.'}</p></div>
      {view === 'venta' && <fieldset className="sale-columns" disabled={checkoutLocked}>
        <legend className="sr-only">Preparación de venta</legend>
        <CatalogPanel state={catalogState} onSearch={catalog.edit} onRetry={catalog.load}
          onAdd={product => updateCart(() => useCases.addToCart(cart, product, 1))} />
        <section className="card cart-panel" aria-labelledby="cart-title">
          <div className="panel-heading"><div><p className="eyebrow">Revisa tu selección</p><h2 id="cart-title">Carrito</h2></div><span className="count-badge">{cart.lines.length} productos</span></div>
          {cart.isEmpty && <div className="empty-state"><strong>Tu carrito está vacío</strong><p>Agrega productos del catálogo para comenzar.</p></div>}
          {cartMessage && <p className="error" role="alert">{cartMessage}</p>}
          <ul className="cart">{cart.lines.map(line => <li key={line.productId}>
            <div className="cart-line-heading"><div><strong>{line.name}</strong><span className="hint">{displayAmount(line.unitPrice)} c/u</span></div><span className="price">{displayAmount(lineSubtotal(line))}</span></div>
            <div className="cart-line-actions"><QuantityInput line={line} onCommit={quantityCallbacks.get(line.productId)} />
              <button type="button" className="danger" onClick={() => updateCart(() => useCases.removeFromCart(cart, line.productId))} aria-label={`Quitar ${line.name}`}>Quitar</button></div>
          </li>)}</ul>
          <div className="cart-summary"><label htmlFor="discount-code">Descuento autorizado</label>
            <select id="discount-code" value={discountCode} onChange={e => setDiscountCode(e.target.value)}>
              {discounts.length === 0 && <option value="none">Sin descuento</option>}
              {discounts.map(discount => <option key={discount.code} value={discount.code}>{discount.name}</option>)}
            </select>
            {discountError && <p className="error" role="alert">{discountError}</p>}
            <dl className="totals"><dt>Subtotal provisional</dt><dd>{displayAmount(currentPreview?.subtotal ?? cart.subtotal)}</dd>
              <dt>Descuento</dt><dd>{currentPreview ? `− ${displayAmount(currentPreview.discount)}` : 'Por verificar'}</dd>
              <dt>Total provisional · sin impuestos</dt><dd className="total">{currentPreview ? displayAmount(currentPreview.total) : 'Por verificar'}</dd></dl>
            <p className="hint" role="status">{previewLoading ? 'Verificando precios y descuento…' : 'El importe final se consulta en el paso Cobro.'}</p>
            {previewError && <p className="error" role="alert">{previewError}</p>}
          </div>
        </section>
      </fieldset>}
      <SalesPanel view={view} useCases={useCases} cart={cart} discountCode={discountCode}
        onLockChange={setCheckoutLocked} onContinue={() => navigation.select('cobro')} onConfirmed={() => setCart(Cart.empty())} />
    </main>
  </div>;
}
