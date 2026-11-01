import { useEffect, useState } from 'react';
import { Cart, lineSubtotal } from '../domain/cart.js';

const money = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' });

function useDebounce(value, delay) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);
  return debounced;
}

export default function PosPage({ useCases, username, onLogout }) {
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

  return (
    <div className="layout">
      <header className="topbar">
        <h1>POS Odoo</h1>
        <span className="muted">Operador: {username}</span>
        <button className="link" onClick={onLogout}>Cerrar sesión</button>
      </header>

      <section className="card">
        <h2>Catálogo</h2>
        <input
          type="search"
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
              <input
                type="number"
                min="1"
                aria-label={`Cantidad de ${line.name}`}
                value={line.quantity}
                onChange={(e) => updateCart(() => useCases.changeQuantity(cart, line.productId, e.target.valueAsNumber))}
              />
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
          {previewLoading ? 'Calculando con Odoo…' : 'Sin IVA: Odoo calcula el impuesto y el total final al registrar la venta (semana 3).'}
        </p>
        {previewError && <p className="error" role="alert">{previewError}</p>}
      </section>
    </div>
  );
}
