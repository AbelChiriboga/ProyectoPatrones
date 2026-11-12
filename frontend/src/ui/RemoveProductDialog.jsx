import SafeDialog from './SafeDialog.jsx';

export default function RemoveProductDialog({ state, onCancel, onConfirm }) {
  if (!state.target) return null;
  return <SafeDialog title="¿Quitar producto del carrito?" description={`${state.target.name} se retirará únicamente de la venta en preparación. No se modifica el catálogo ni Odoo.`} onDismiss={onCancel}>
    <div className="dialog-body">{state.error && <p className="error" role="alert">{state.error}</p>}<p>Cancelar conserva el carrito y la cotización vigente.</p></div>
    <footer className="dialog-actions"><button type="button" className="secondary" data-dialog-initial-focus disabled={state.busy} onClick={onCancel}>Cancelar</button>
      <button type="button" className="remove-confirm" disabled={state.busy} onClick={onConfirm}>Sí, quitar producto</button></footer>
  </SafeDialog>;
}
