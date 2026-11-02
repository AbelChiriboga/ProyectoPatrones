import { parseCatalogSearch } from './catalogSearch.js';

// Casos de uso del frontend. Dependen de dos puertos que se inyectan desde fuera:
//
//   api     -> { login({username, password}), searchProducts(term), listDiscounts(), previewSale({lines, discountCode}) }
//   session -> { read(), save(token, username), clear() }
//
// No importan React ni fetch (RNF-02): se pueden probar con dobles y cambiar de framework sin tocarlos.

export class UseCaseError extends Error {
  constructor(code, message) {
    super(message);
    this.name = 'UseCaseError';
    this.code = code;
  }
}

const EMPTY_PREVIEW = Object.freeze({ lines: [], subtotal: 0, discountCode: 'none', discount: 0, total: 0 });

export function createUseCases({ api, session, pending }) {
  return {
    activeSession: () => api.activeSession(),
    quoteSale: (sale) => api.quoteSale(sale),
    pendingSale: () => pending?.read(session.read()?.username),
    async confirmSale(sale, paymentMethodId, expectedTotal) {
      const owner = session.read()?.username;
      if (pending.read(owner)) throw new UseCaseError('sale.pending', 'Consulte primero la operación pendiente.');
      const request = { operationId: pending.newId(), sale, paymentMethodId, expectedTotal };
      pending.save(owner, request);
      try {
        const result = await api.registerSale(request);
        if (result.status === 'confirmed') pending.clear(owner);
        return result;
      } catch (error) {
        // Only a definitive pre-send validation allows correcting the cart with a fresh identity.
        if ([400, 401, 403, 404, 422].includes(error.status) || error.code === 'sale.total_changed') pending.clear(owner);
        throw error;
      }
    },
    async reconcileSale() {
      const owner = session.read()?.username;
      const request = pending.read(owner);
      if (!request) throw new UseCaseError('sale.no_pending', 'No hay operación pendiente.');
      const result = await api.saleOperation(request.operationId);
      if (result.status === 'confirmed') pending.clear(owner);
      return result;
    },
    salesHistory: (beforeId) => api.salesHistory(beforeId),
    saleDetail: (id) => api.saleDetail(id),
    currentUser() {
      return session.read()?.username ?? null;
    },

    async login(username, password) {
      if (!username?.trim() || !password) {
        throw new UseCaseError('auth.credentials_required', 'Ingrese usuario y contraseña.');
      }
      const result = await api.login({ username: username.trim(), password });
      session.save(result.token, result.username);
      return result.username;
    },

    logout() {
      session.clear();
    },

    searchProducts(term) {
      return api.searchProducts(parseCatalogSearch(term ?? ''));
    },

    listDiscounts() {
      return api.listDiscounts();
    },

    // Operaciones del carrito (RF-03 a RF-05): las reglas viven en el dominio (Cart).
    addToCart(cart, product, quantity = 1) {
      return cart.addProduct(product, quantity);
    },

    changeQuantity(cart, productId, quantity) {
      return cart.changeQuantity(productId, quantity);
    },

    removeFromCart(cart, productId) {
      return cart.removeProduct(productId);
    },

    // Total provisional (P07/P08): la API calcula con los precios de Odoo y aplica solo descuentos autorizados.
    async previewSale(cart, discountCode) {
      if (cart.isEmpty) {
        return EMPTY_PREVIEW;
      }
      const code = discountCode && discountCode !== 'none' ? discountCode : null;
      return api.previewSale({ lines: cart.toRequestLines(), discountCode: code });
    },
  };
}
