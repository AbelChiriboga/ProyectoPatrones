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

export function createUseCases({ api, session }) {
  return {
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
      return api.searchProducts((term ?? '').trim());
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
