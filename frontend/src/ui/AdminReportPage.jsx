import { useState, useEffect, useCallback } from 'react';

/**
 * Vista de reportes globales de ventas — solo visible para el rol Administrador.
 * Consulta GET /api/reports/sales con filtros opcionales de fecha.
 * No realiza ninguna escritura ni accede directamente a Odoo.
 */
export default function AdminReportPage({ useCases, username, onLogout }) {
  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  const loadReport = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await useCases.getSalesReport({
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to + 'T23:59:59').toISOString() : undefined,
      });
      setReport(data);
    } catch (e) {
      setError(e.message ?? 'Error al cargar el reporte.');
    } finally {
      setLoading(false);
    }
  }, [useCases, from, to]);

  // Carga inicial sin filtros
  useEffect(() => {
    loadReport();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fmt = (n) =>
    typeof n === 'number' ? n.toLocaleString('es-EC', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—';

  return (
    <main className="admin-report">
      <header className="admin-report__header card">
        <div>
          <h1>Reportes — Administrador</h1>
          <p className="muted">Usuario: <strong>{username}</strong></p>
        </div>
        <button onClick={onLogout} className="btn-logout">Cerrar sesión</button>
      </header>

      <section className="card admin-report__filters">
        <h2>Filtrar por fecha</h2>
        <div className="admin-report__filter-row">
          <label>
            Desde
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label>
            Hasta
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
          <button onClick={loadReport} disabled={loading}>
            {loading ? 'Cargando…' : 'Consultar'}
          </button>
        </div>
        {error && <p className="error" role="alert">{error}</p>}
      </section>

      {report && (
        <>
          <section className="card admin-report__summary">
            <h2>Resumen global</h2>
            <dl className="admin-report__kpi">
              <div>
                <dt>Total de órdenes</dt>
                <dd>{report.totalOrders}</dd>
              </div>
              <div>
                <dt>Ventas totales</dt>
                <dd>{fmt(report.grandTotal)} {report.currency}</dd>
              </div>
            </dl>
          </section>

          <section className="card admin-report__by-cashier">
            <h2>Desglose por cajero</h2>
            {report.byCashier?.length > 0 ? (
              <table>
                <thead>
                  <tr>
                    <th>Cajero</th>
                    <th className="num">Órdenes</th>
                    <th className="num">Total ({report.currency})</th>
                  </tr>
                </thead>
                <tbody>
                  {report.byCashier.map((row) => (
                    <tr key={row.cashier}>
                      <td>{row.cashier}</td>
                      <td className="num">{row.orderCount}</td>
                      <td className="num">{fmt(row.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="muted">Sin datos para el período seleccionado.</p>
            )}
          </section>

          <section className="card admin-report__by-method">
            <h2>Desglose por método de pago</h2>
            {report.byPaymentMethod?.length > 0 ? (
              <table>
                <thead>
                  <tr>
                    <th>Método</th>
                    <th className="num">Transacciones</th>
                    <th className="num">Total ({report.currency})</th>
                  </tr>
                </thead>
                <tbody>
                  {report.byPaymentMethod.map((row) => (
                    <tr key={row.method}>
                      <td>{row.method}</td>
                      <td className="num">{row.count}</td>
                      <td className="num">{fmt(row.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="muted">Sin datos para el período seleccionado.</p>
            )}
          </section>
        </>
      )}
    </main>
  );
}
