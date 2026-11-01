// Dominio del carrito (RF-03 a RF-05). JavaScript puro: no conoce React ni HTTP (RNF-02).
// El carrito es inmutable: cada operación devuelve un carrito nuevo.

export class CartError extends Error {
  constructor(code, message) {
    super(message);
    this.name = 'CartError';
    this.code = code;
  }
}

// Se suma en centavos para evitar errores de coma flotante (0.1 + 0.2).
const toCents = (amount) => Math.round(amount * 100);

export const lineSubtotal = (line) => (toCents(line.unitPrice) * line.quantity) / 100;

function assertValidQuantity(quantity) {
  if (!Number.isInteger(quantity) || quantity <= 0) {
    throw new CartError('cart.invalid_quantity', 'La cantidad debe ser un número entero mayor que cero.');
  }
}

// Solo se valida el stock cuando Odoo informa una disponibilidad fiable (RF-02).
function assertStock(line) {
  if (line.quantityAvailable != null && line.quantity > line.quantityAvailable) {
    throw new CartError('cart.insufficient_stock', `Solo hay ${line.quantityAvailable} unidades de ${line.name}.`);
  }
}

export class Cart {
  constructor(lines = []) {
    this.lines = Object.freeze([...lines]);
    Object.freeze(this);
  }

  static empty() {
    return new Cart();
  }

  get isEmpty() {
    return this.lines.length === 0;
  }

  // Total provisional sin impuestos ni descuento; el total oficial lo calcula la API con los precios de Odoo.
  get subtotal() {
    return this.lines.reduce((sum, line) => sum + toCents(line.unitPrice) * line.quantity, 0) / 100;
  }

  addProduct(product, quantity = 1) {
    assertValidQuantity(quantity);

    const existing = this.lines.find((line) => line.productId === product.id);
    if (existing) {
      return this.changeQuantity(product.id, existing.quantity + quantity);
    }

    const line = {
      productId: product.id,
      code: product.code ?? null,
      name: product.name,
      unitPrice: product.unitPrice,
      quantityAvailable: product.quantityAvailable ?? null,
      quantity,
    };
    assertStock(line);
    return new Cart([...this.lines, line]);
  }

  changeQuantity(productId, quantity) {
    assertValidQuantity(quantity);
    const existing = this.#find(productId);

    const updated = { ...existing, quantity };
    assertStock(updated);
    return new Cart(this.lines.map((line) => (line.productId === productId ? updated : line)));
  }

  removeProduct(productId) {
    this.#find(productId);
    return new Cart(this.lines.filter((line) => line.productId !== productId));
  }

  // Formato que espera POST /api/sales/preview.
  toRequestLines() {
    return this.lines.map((line) => ({ productId: line.productId, quantity: line.quantity }));
  }

  #find(productId) {
    const line = this.lines.find((l) => l.productId === productId);
    if (!line) {
      throw new CartError('cart.line_not_found', 'El producto no está en el carrito.');
    }
    return line;
  }
}
