import { useId, useLayoutEffect, useRef } from 'react';
import { activateDialog, cancelDialog, containDialogFocus, ignoreDialogBackdrop } from './dialogBehavior.js';

export default function SafeDialog({ title, description, onDismiss, children }) {
  const dialog = useRef(null);
  const id = useId();
  useLayoutEffect(() => activateDialog(dialog.current), []);
  return <dialog ref={dialog} className="safe-dialog" role="dialog" aria-modal="true"
    aria-labelledby={`${id}-title`} aria-describedby={`${id}-description`}
    onCancel={event => cancelDialog(event, onDismiss)} onKeyDown={event => containDialogFocus(event, dialog.current)}
    onClick={ignoreDialogBackdrop} onPointerDown={ignoreDialogBackdrop}>
    <header className="dialog-heading"><h2 id={`${id}-title`}>{title}</h2><p id={`${id}-description`}>{description}</p></header>
    {children}
  </dialog>;
}
