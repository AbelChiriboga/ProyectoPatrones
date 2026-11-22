import { describe, expect, it, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import AdminReportPage from './AdminReportPage.jsx';

describe('AdminReportPage', () => {
  it('renders report header with username and filter controls', () => {
    const useCases = {
      getSalesReport: vi.fn().mockResolvedValue({
        totalOrders: 10,
        grandTotal: 150.5,
        currency: 'USD',
        byCashier: [{ cashier: 'cajero1', orderCount: 5, total: 75.25 }],
        byPaymentMethod: [{ method: 'Efectivo', count: 10, total: 150.5 }],
      }),
    };

    const html = renderToStaticMarkup(
      <AdminReportPage useCases={useCases} username="admin1" onLogout={() => {}} />
    );

    expect(html).toContain('Reportes — Administrador');
    expect(html).toContain('admin1');
    expect(html).toContain('Filtrar por fecha');
    expect(html).toContain('Cerrar sesión');
  });
});
