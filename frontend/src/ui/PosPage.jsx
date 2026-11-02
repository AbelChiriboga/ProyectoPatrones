import { useEffect, useState, useCallback, useMemo, useSyncExternalStore } from 'react';
import { Cart, lineSubtotal } from '../domain/cart.js';
import SalesPanel from './SalesPanel.jsx';
import QuantityInput from './QuantityInput.jsx';
import { createViewNavigation, browserHashPort, viewFromHash } from './viewNavigation.js';

const money = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' });

function useDebounce(value, delay) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);
  return debounced;
}

export default function PosPage({ useCases, username, onLogout, initialView = 'venta' }) {
  const navigation = useMemo(() => createViewNavigation(browserHashPort), []);
  const view = useSyncExternalStore(navigation.subscribe, navigation.read, () => viewFromHash(initialView));
  const [checkoutLocked, setCheckoutLocked] = useState(false);
  const [term, setTerm] = useState('');
  const debouncedTerm = useDebounce(term, 300);
  const [products, setProducts] = useState([]);
  const [catalogError, setCatalogError] = useState('');
  const [catalogLoading, setCatalogLoading] = useState(false);

  const [cart, setCart] = useState(Cart.empty());
  const [cartMessage, setCartMessage] = useState('');

  const [discounts, setDiscounts] = useState([]);
  const [discountCode, setDiscountCode] = useState('none');
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState('');
  const [previewLoading, setPreviewLoading] = useState(false);

  // RF-01 / RF-02: catálogo y búsqueda por nombre o código.
  useEffect(() => {
    let cancelled = false;
    setCatalogLoading(true);
    useCases
      .searchProducts(debouncedTerm)
      .then((result) => !cancelled && (setProducts(result), setCatalogError('')))
      .catch((e) => !cancelled && setCatalogError(e.message))
      .finally(() => !cancelled && setCatalogLoading(false));
    return () => {
      cancelled = true;
    };
  }, [useCases, debouncedTerm]);

  // RF-06 a RF-08: solo se ofrecen las políticas que la API autoriza.
  useEffect(() => {
    useCases.listDiscounts().then(setDiscounts).catch(() => setDiscounts([]));
  }, [useCases]);

  // Total provisional: se recalcula en la API cada vez que cambia el carrito o el descuento.
  useEffect(() => {
    let cancelled = false;
    setPreviewLoading(true);
    useCases
      .previewSale(cart, discountCode)
      .then((result) => !cancelled && (setPreview(result), setPreviewError('')))
      .catch((e) => !cancelled && (setPreview(null), setPreviewError(e.message)))
      .finally(() => !cancelled && setPreviewLoading(false));
    return () => {
      cancelled = true;
    };
  }, [useCases, cart, discountCode]);

  // Las reglas del carrito viven en el dominio; la interfaz solo muestra el mensaje si se incumplen.
  const updateCart = (operation) => {
    try {
      setCart(operation());
      setCartMessage('');
    } catch (e) {
      setCartMessage(e.message);
    }
  };

  const commitQuantity = useCallback((id, quantity) => {
    const current = cart.lines.find(line => line.productId === id);
    if (current.quantity === quantity) return;
    const updated = useCases.changeQuantity(cart, id, quantity);
    setCart(updated);
  }, [cart, useCases]);
  const quantityCallbacks = useMemo(() => new Map(cart.lines.map(line =>
    [line.productId, quantity => commitQuantity(line.productId, quantity)])), [cart, commitQuantity]);
  const commitQuantityFor = id => quantityCallbacks.get(id);

  return (
    <div className="layout">
      <header className="topbar">
        <h1>POS Odoo</h1>
        <span className="muted">Operador: {username}</span>
        <button className="link" onClick={onLogout}>Cerrar sesión</button>
      </header>

      <nav className="areas" aria-label="Áreas del POS"><a href="#venta" aria-current={view === 'venta' ? 'page' : undefined}>Venta</a><a href="#historial" aria-current={view === 'historial' ? 'page' : undefined}>Historial</a><a href="#operaciones" aria-current={view === 'operaciones' ? 'page' : undefined}>Operaciones pendientes</a></nav>
      {view === 'venta' && <fieldset disabled={checkoutLocked} style={{ display: 'contents' }}>
      <section className="card" id="venta" tabIndex="-1">
        <h2>Venta · Catálogo</h2>
        <input
          type="search"
          aria-label="Buscar productos"
          placeholder="Buscar por nombre o código…"
          value={term}
          onChange={(e) => setTerm(e.target.value)}
        />
        {catalogError && <p className="error" role="alert">{catalogError}</p>}
        {catalogLoading && <p className="muted">Cargando…</p>}
        {!catalogLoading && !catalogError && products.length === 0 && <p className="muted">No hay productos para mostrar.</p>}

        <ul className="products">
          {products.map((product) => (
            <li key={product.id}>
              <div>
                <strong>{product.name}</strong>
                <span className="muted">
                  {product.code ?? 'sin código'}
                  {product.quantityAvailable != null && ` · ${product.quantityAvailable} disponibles`}
                </span>
              </div>
              <span className="price">{money.format(product.unitPrice)}</span>
              <button onClick={() => updateCart(() => useCases.addToCart(cart, product, 1))}>Agregar</button>
            </li>
          ))}
        </ul>
      </section>

      <section className="card">
        <h2>Carrito</h2>
        {cart.isEmpty && <p className="muted">El carrito está vacío.</p>}
        {cartMessage && <p className="error" role="alert">{cartMessage}</p>}

        <ul className="cart">
          {cart.lines.map((line) => (
            <li key={line.productId}>
              <div>
                <strong>{line.name}</strong>
                <span className="muted">{money.format(line.unitPrice)} c/u</span>
              </div>
              <QuantityInput line={line} onCommit={commitQuantityFor(line.productId)} />
              <span className="price">{money.format(lineSubtotal(line))}</span>
              <button className="danger" onClick={() => updateCart(() => useCases.removeFromCart(cart, line.productId))}>
                Quitar
              </button>
            </li>
          ))}
        </ul>

        <label>
          Descuento
          <select value={discountCode} onChange={(e) => setDiscountCode(e.target.value)}>
            {discounts.length === 0 && <option value="none">Sin descuento</option>}
            {discounts.map((d) => (
              <option key={d.code} value={d.code}>{d.name}</option>
            ))}
          </select>
        </label>

        <dl className="totals">
          <dt>Subtotal</dt>
          <dd>{money.format(preview?.subtotal ?? cart.subtotal)}</dd>
          <dt>Descuento</dt>
          <dd>− {money.format(preview?.discount ?? 0)}</dd>
          <dt>Total provisional</dt>
          <dd className="total">{preview ? money.format(preview.total) : '—'}</dd>
        </dl>
        <p className="muted small">
          {previewLoading ? 'Calculando con Odoo…' : 'Total provisional sin impuestos. Consulte el total con impuestos en la revisión de efectivo antes de confirmar.'}
        </p>
        {previewError && <p className="error" role="alert">{previewError}</p>}
      </section>
      </fieldset>}
      <SalesPanel view={view} useCases={useCases} cart={cart} discountCode={discountCode} onLockChange={setCheckoutLocked} onConfirmed={() => setCart(Cart.empty())} />
    </div>
  );
}
