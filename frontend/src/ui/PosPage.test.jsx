import { describe, expect, it } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import PosPage from './PosPage.jsx';

// Server rendering only: no browser, DOM emulator, HTTP requests or sales.
describe('week 3 is mounted in the POS', () => {
  it('includes checkout and history alongside the existing cart', () => {
    const html = renderToStaticMarkup(<PosPage useCases={{ pendingSale: () => null }} username="operador" onLogout={() => {}} />);
    expect(html).toContain('Catálogo');
    expect(html).toContain('Carrito');
    expect(html).toContain('Cobro e historial');
    expect(html).toContain('Consultar total con impuestos');
    expect(html).toContain('Ver historial');
    expect(html).not.toContain('al registrar la venta (semana 3)');
  });

  it('makes reconciliation accessible for a pending operation after remount', () => {
    const html = renderToStaticMarkup(<PosPage useCases={{ pendingSale: () => ({ operationId: 'pending-test' }) }} username="operador" onLogout={() => {}} />);
    expect(html).toContain('pending-test');
    expect(html).toContain('Consultar operación pendiente');
    expect(html).toContain('Ver historial');
  });
});
