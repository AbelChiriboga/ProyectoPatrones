import { useRef } from 'react';

const price = value => new Intl.NumberFormat('es-EC', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
export default function CatalogPanel({ state, onSearch, onRetry, onAdd }) {
  const composing = useRef(false);
  const ready = state.status === 'ready';
  return <section className="card catalog-panel" aria-labelledby="catalog-title">
    <div className="panel-heading"><div><p className="eyebrow">Elige los productos</p><h2 id="catalog-title">Catálogo</h2></div></div>
    <label className="input-label" htmlFor="product-search">Buscar producto</label>
    <input id="product-search" className="search-input" type="search" aria-label="Buscar productos"
      placeholder="Nombre o código, ej. CAFE-001" value={state.term}
      aria-invalid={!!state.inputError} aria-describedby={`search-help${state.inputError ? ' search-error' : ''}`}
      onCompositionStart={() => { composing.current = true; }}
      onCompositionEnd={e => { composing.current = false; onSearch(e.currentTarget.value); }}
      onChange={e => { if (!composing.current && !e.nativeEvent.isComposing) onSearch(e.target.value); }} />
    <p className="hint" id="search-help">Nombre o código · Hasta 100 caracteres. Agrega con el botón de cada producto.</p>
    {state.inputError && <p className="error field-error" role="alert" id="search-error">{state.inputError} Se conserva la búsqueda válida anterior.</p>}
    {(state.status === 'idle' || state.status === 'loading') && !state.inputError && <p className="empty-state" role="status">Consultando catálogo…</p>}
    {state.status === 'error' && <div className="notice notice-error" role="alert"><p>{state.networkError}</p><button className="secondary" onClick={onRetry}>Reintentar catálogo</button></div>}
    {ready && <>
      {state.inputError && <p className="hint">Resultados conservados de {state.searchedTerm ? `«${state.searchedTerm}»` : 'la consulta de catálogo'}.</p>}
      {!state.inputError && <p className="result-count" role="status">{state.products.length ? `${state.products.length} productos mostrados` : state.searchedTerm
        ? `No se encontraron productos para «${state.searchedTerm}».` : 'No hay productos disponibles en el catálogo.'}</p>}
      <ul className="products">{state.products.map(product => <li key={product.id}>
        <div className="product-name"><strong>{product.name}</strong><span className="product-code">{product.code ?? 'Sin código'}</span></div>
        <span className="price" aria-label="Precio de catálogo">{price(product.unitPrice)}</span>
        <button type="button" onClick={() => onAdd(product)}>Agregar</button>
      </li>)}</ul>
    </>}
  </section>;
}
