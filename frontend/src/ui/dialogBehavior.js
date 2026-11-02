// Small DOM adapter exercised with focus ports in Node tests, never a browser driver.
const focusable = 'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
export function activateDialog(dialog) {
  const doc = dialog.ownerDocument;
  const opener = doc.activeElement;
  const overflow = doc.body.style.overflow;
  const view = doc.defaultView;
  const position = view ? [view.scrollX, view.scrollY] : null;
  doc.body.style.overflow = 'hidden';
  if (!dialog.open) dialog.showModal();
  const initial = dialog.querySelector('[data-dialog-initial-focus]') ?? dialog;
  initial.focus({ preventScroll: true });
  return () => {
    if (dialog.open) dialog.close();
    doc.body.style.overflow = overflow;
    const target = opener?.isConnected ? opener : doc.querySelector('main h1');
    target?.focus({ preventScroll: true });
    if (position) view.scrollTo(...position);
  };
}
export function containDialogFocus(event, dialog) {
  if (event.key !== 'Tab') return;
  const elements = Array.from(dialog.querySelectorAll(focusable)).filter(node => !node.disabled && node.tabIndex >= 0 && !node.hidden && !node.closest('[hidden], [inert]'));
  const first = elements[0], last = elements.at(-1), active = dialog.ownerDocument.activeElement;
  if (!first) { event.preventDefault(); dialog.focus({ preventScroll: true }); return; }
  if ((event.shiftKey && (active === first || !elements.includes(active))) ||
      (!event.shiftKey && (active === last || !elements.includes(active)))) {
    event.preventDefault(); (event.shiftKey ? last : first).focus({ preventScroll: true });
  }
}
export function cancelDialog(event, dismiss) { event.preventDefault(); dismiss(); }
export function ignoreDialogBackdrop(event) {
  if (event.target === event.currentTarget) event.preventDefault();
}
