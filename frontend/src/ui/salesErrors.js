export const UNCERTAIN_SALE_MESSAGE =
  'Resultado incierto. No vuelva a cobrar; consulte la operación pendiente con el mismo identificador.';

export function mapSaleFailureMessage(error, { readOnly = false, hasPending = false } = {}) {
  if (!readOnly && hasPending) return UNCERTAIN_SALE_MESSAGE;
  if (error.code?.startsWith('session.')) {
    return 'La sesión POS no está abierta o cambió. Solicite abrir la caja y vuelva a cotizar.';
  }
  if (error.code?.startsWith('products.')) {
    return 'Producto no disponible para el POS. Revise el carrito y vuelva a cotizar.';
  }
  if (error.code === 'sale.total_changed') {
    return 'El total cambió. Consulte una nueva cotización antes de cobrar.';
  }
  if (error.status === 401) return 'Inicie sesión nuevamente para continuar.';
  if (error.status === 422 || error.status === 400) {
    return 'La operación fue rechazada por datos inválidos. Revise cantidades, descuento y método; vuelva a cotizar.';
  }
  return 'No se pudo obtener un resultado verificado. Revise la conexión y consulte nuevamente; no repita un cobro pendiente.';
}

export function classifySaleOutcome(error, { readOnly = false, hasPending = false } = {}) {
  if (!readOnly && hasPending) return 'uncertain';
  const isRejection = [400, 409, 422].includes(error.status) ||
    error.code?.startsWith('session.') ||
    error.code?.startsWith('products.');
  return isRejection ? 'rejected' : 'error';
}
