import { describe, expect, it } from 'vitest';
import { Cart, CartError } from './cart.js';

const coffee = { id: 11, code: 'CAFE-001', name: 'Café americano', unitPrice: 1.5, quantityAvailable: null };
const sandwich = { id: 12, code: 'SAND-001', name: 'Sánduche de pollo', unitPrice: 3.75, quantityAvailable: null };
const water = { id: 20, code: null, name: 'Botella de agua', unitPrice: 0.75, quantityAvailable: 3 };

describe('Cart', () => {
  it('starts empty', () => {
    expect(Cart.empty().isEmpty).toBe(true);
    expect(Cart.empty().subtotal).toBe(0);
  });

  it('adds products and calculates the provisional subtotal (P07)', () => {
    const cart = Cart.empty().addProduct(coffee, 2).addProduct(sandwich, 1);

    expect(cart.lines).toHaveLength(2);
    expect(cart.subtotal).toBe(6.75);
  });

  it('merges the same product into one line', () => {
    const cart = Cart.empty().addProduct(coffee, 1).addProduct(coffee, 2);

    expect(cart.lines).toHaveLength(1);
    expect(cart.lines[0].quantity).toBe(3);
  });

  it('changes quantities and recalculates (P08)', () => {
    const cart = Cart.empty().addProduct(coffee, 1).changeQuantity(11, 4);

    expect(cart.lines[0].quantity).toBe(4);
    expect(cart.subtotal).toBe(6);
  });

  it('removes a product', () => {
    const cart = Cart.empty().addProduct(coffee, 1).addProduct(sandwich, 1).removeProduct(11);

    expect(cart.lines.map((l) => l.productId)).toEqual([12]);
  });

  it('does not accumulate floating point errors', () => {
    const cheap = { id: 1, name: 'A', unitPrice: 0.1, quantityAvailable: null };
    const cheaper = { id: 2, name: 'B', unitPrice: 0.2, quantityAvailable: null };

    expect(Cart.empty().addProduct(cheap).addProduct(cheaper).subtotal).toBe(0.3);
  });

  it.each([0, -1, 1.5, Number.NaN])('rejects invalid quantity %s', (quantity) => {
    expect(() => Cart.empty().addProduct(coffee, quantity)).toThrow(CartError);
  });

  it('rejects changing the quantity to zero', () => {
    const cart = Cart.empty().addProduct(coffee, 1);

    expect(() => cart.changeQuantity(11, 0)).toThrowError(expect.objectContaining({ code: 'cart.invalid_quantity' }));
  });

  it('fails when the product is not in the cart', () => {
    expect(() => Cart.empty().removeProduct(99)).toThrowError(expect.objectContaining({ code: 'cart.line_not_found' }));
    expect(() => Cart.empty().changeQuantity(99, 1)).toThrowError(expect.objectContaining({ code: 'cart.line_not_found' }));
  });

  it('does not allow more units than the reliable stock', () => {
    const cart = Cart.empty().addProduct(water, 3);

    expect(() => cart.addProduct(water, 1)).toThrowError(expect.objectContaining({ code: 'cart.insufficient_stock' }));
    expect(() => Cart.empty().addProduct(water, 4)).toThrowError(expect.objectContaining({ code: 'cart.insufficient_stock' }));
  });

  it('never mutates the previous cart', () => {
    const original = Cart.empty().addProduct(coffee, 1);
    original.addProduct(sandwich, 1);

    expect(original.lines).toHaveLength(1);
  });

  it('builds the request lines for the API', () => {
    const cart = Cart.empty().addProduct(coffee, 2).addProduct(sandwich, 1);

    expect(cart.toRequestLines()).toEqual([
      { productId: 11, quantity: 2 },
      { productId: 12, quantity: 1 },
    ]);
  });
});
