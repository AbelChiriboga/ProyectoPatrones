import { useMemo, useRef, useState } from 'react';
import { createQuantityEditor, commitQuantityControl } from './quantityEditor.js';

export default function QuantityInput({ line, onCommit }) {
  const editor = useMemo(() => createQuantityEditor(line.quantity, onCommit), [line.quantity, onCommit]);
  const [view, setView] = useState(null);
  const composing = useRef(false);
  const current = view?.editor === editor ? view.state : editor.read();
  const update = state => setView({ editor, state });
  const commit = event => {
    if (composing.current) return;
    const next = commitQuantityControl(editor, event);
    if (next) update(next);
  };
  const id = `quantity-${line.productId}`;
  return <div className="quantity-editor">
    <div className="quantity-controls">
      <button type="button" className="quantity-button" aria-label={`Disminuir cantidad de ${line.name}`}
        disabled={line.quantity <= 1} onClick={() => update(editor.step(-1))}>−</button>
      <input id={id} type="text" inputMode="numeric" pattern="[0-9]*" autoComplete="off" aria-label={`Cantidad de ${line.name}`}
        aria-invalid={!!current.error && !current.restored} aria-describedby={current.error ? `${id}-error` : undefined}
        value={current.draft} onCompositionStart={() => { composing.current = true; }}
        onCompositionEnd={e => { composing.current = false; update(editor.edit(e.currentTarget.value)); }}
        onChange={e => { if (!composing.current && !e.nativeEvent.isComposing) update(editor.edit(e.target.value)); }}
        onBlur={commit} onKeyDown={commit} />
      <button type="button" className="quantity-button" aria-label={`Aumentar cantidad de ${line.name}`}
        disabled={line.quantity >= Number.MAX_SAFE_INTEGER || (line.quantityAvailable != null && line.quantity >= line.quantityAvailable)}
        onClick={() => update(editor.step(1))}>+</button>
    </div>
    {current.error && <span id={`${id}-error`} className={current.restored ? 'hint field-error' : 'error field-error'} role={current.restored ? 'status' : 'alert'}>
      {current.restored ? `Entrada descartada. Se conserva la cantidad confirmada: ${current.draft}. ${current.error}` : current.error}
    </span>}
  </div>;
}
