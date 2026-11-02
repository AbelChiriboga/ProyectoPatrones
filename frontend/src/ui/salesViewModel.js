// Display-only projections of backend DTOs. No fiscal rules or inferred business data.
const labels = { paid: 'Pagada', done: 'Contabilizada', draft: 'Borrador', cancel: 'Cancelada' };
export const orderStateLabel = state => labels[state] ?? 'Estado no reconocido';
export function displayAmount(value, currency) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return 'No informado';
  const amount = new Intl.NumberFormat('es-EC', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  return currency ? `${amount} ${currency}` : amount;
}
export function quoteSummary(quote) {
  const lines = quote?.lines;
  if (!lines?.length) return { subtotal: null, discount: null, tax: null, total: quote?.total };
  const subtotal = lines.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
  const net = lines.reduce((sum, line) => sum + line.net, 0);
  return { subtotal, discount: subtotal - net, tax: lines.reduce((sum, line) => sum + line.tax, 0), total: quote.total };
}
