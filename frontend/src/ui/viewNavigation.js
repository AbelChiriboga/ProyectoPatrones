// Minimal hash routing. Only presentation selection changes; cart/checkout stay in the parent.
export const viewFromHash = hash => ['venta', 'cobro', 'historial', 'operaciones'].includes(hash?.replace(/^#/, ''))
  ? hash.replace(/^#/, '') : 'venta';
export function createViewNavigation(port) {
  return {
    read: () => viewFromHash(port.readHash()),
    subscribe: listener => port.onHashChange(listener),
    select: view => port.writeHash(`#${viewFromHash(view)}`),
  };
}
export const browserHashPort = {
  readHash: () => typeof window === 'undefined' ? '#venta' : window.location.hash,
  onHashChange(listener) {
    if (typeof window === 'undefined') return () => {};
    window.addEventListener('hashchange', listener);
    return () => window.removeEventListener('hashchange', listener);
  },
  writeHash(hash) { window.location.hash = hash; },
};
