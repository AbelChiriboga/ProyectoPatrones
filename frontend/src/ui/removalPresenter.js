// Confirmation policy only: Application/Cart still perform the actual business operation.
export function createRemovalPresenter({ readCart, remove, canRemove = () => true }) {
  let state = { target: null, busy: false, error: '' };
  const listeners = new Set();
  const patch = values => { state = { ...state, ...values }; listeners.forEach(fn => fn()); };
  return {
    read: () => state,
    subscribe(fn) { listeners.add(fn); return () => listeners.delete(fn); },
    request(id) {
      if (state.target || state.busy || !canRemove()) return false;
      const line = readCart().lines.find(item => item.productId === id);
      if (!line) return false;
      patch({ target: { productId: line.productId, name: line.name }, error: '' });
      return true;
    },
    cancel() { if (state.target && !state.busy) patch({ target: null, error: '' }); },
    confirm() {
      if (!state.target || state.busy) return false;
      if (!canRemove()) { patch({ error: 'Hay una operación en curso. Cancele y consulte su resultado antes de editar.' }); return false; }
      const id = state.target.productId;
      patch({ busy: true, error: '' });
      try {
        if (!readCart().lines.some(line => line.productId === id)) throw new Error('missing');
        remove(id);
        patch({ target: null, busy: false });
        return true;
      } catch {
        patch({ busy: false, error: 'No se pudo quitar el producto. Cancele y revise la venta en preparación.' });
        return false;
      }
    },
  };
}
