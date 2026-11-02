import { useMemo, useState } from 'react';
import { createQuantityEditor } from './quantityEditor.js';

export default function QuantityInput({ line, onCommit }) {
  const editor = useMemo(() => createQuantityEditor(line.quantity, onCommit), [line.quantity, onCommit]);
  const [view, setView] = useState(null);
  const current = view?.editor === editor ? view.state : editor.read();
  const update = state => setView({ editor, state });
  const id = `quantity-${line.productId}`;
  return <div className="quantity-editor">
    <input id={id} type="text" inputMode="numeric" aria-label={`Cantidad de ${line.name}`}
      aria-invalid={!!current.error} aria-describedby={current.error ? `${id}-error` : undefined}
      value={current.draft} onChange={e => update(editor.edit(e.target.value))}
      onBlur={() => update(editor.commit())}
      onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); e.currentTarget.blur(); } }} />
    {current.error && <span id={`${id}-error`} className="error" role="alert">{current.error}</span>}
  </div>;
}
