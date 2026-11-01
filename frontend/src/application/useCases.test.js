import { describe, expect, it, vi } from 'vitest';
import { Cart } from '../domain/cart.js';
import { createUseCases, UseCaseError } from './useCases.js';

const coffee = { id: 11, code: 'CAFE-001', name: 'Café americano', unitPrice: 1.5, quantityAvailable: null };

function setup() {
  const api = {
    login: vi.fn().mockResolvedValue({ token: 'jwt', username: 'cajero1' }),
    searchProducts: vi.fn().mockResolvedValue([coffee]),
    listDiscounts: vi.fn().mockResolvedValue([{ code: 'none', name: 'Sin descuento' }]),
    previewSale: vi.fn().mockResolvedValue({ lines: [], subtotal: 3, discountCode: 'promo-10', discount: 0.3, total: 2.7 }),
  };
  const session = { read: vi.fn().mockReturnValue(null), save: vi.fn(), clear: vi.fn() };
  return { api, session, useCases: createUseCases({ api, session }) };
}

describe('use cases', () => {
  it('login stores the session with the trimmed username', async () => {
    const { api, session, useCases } = setup();

    await expect(useCases.login(' cajero1 ', 'clave')).resolves.toBe('cajero1');

    expect(api.login).toHaveBeenCalledWith({ username: 'cajero1', password: 'clave' });
    expect(session.save).toHaveBeenCalledWith('jwt', 'cajero1');
  });

  it('login rejects blank credentials without calling the API', async () => {
    const { api, useCases } = setup();

    await expect(useCases.login('  ', 'clave')).rejects.toBeInstanceOf(UseCaseError);
    await expect(useCases.login('cajero1', '')).rejects.toBeInstanceOf(UseCaseError);
    expect(api.login).not.toHaveBeenCalled();
  });

  it('logout clears the session', () => {
    const { session, useCases } = setup();
    useCases.logout();
    expect(session.clear).toHaveBeenCalled();
  });

  it('searchProducts trims the term and tolerates undefined', async () => {
    const { api, useCases } = setup();

    await useCases.searchProducts('  café ');
    await useCases.searchProducts(undefined);

    expect(api.searchProducts).toHaveBeenNthCalledWith(1, 'café');
    expect(api.searchProducts).toHaveBeenNthCalledWith(2, '');
  });

  it('previewSale does not call the API for an empty cart', async () => {
    const { api, useCases } = setup();

    const preview = await useCases.previewSale(Cart.empty(), 'promo-10');

    expect(api.previewSale).not.toHaveBeenCalled();
    expect(preview.total).toBe(0);
  });

  it('previewSale sends the cart lines and the discount code', async () => {
    const { api, useCases } = setup();
    const cart = useCases.addToCart(Cart.empty(), coffee, 2);

    const preview = await useCases.previewSale(cart, 'promo-10');

    expect(api.previewSale).toHaveBeenCalledWith({ lines: [{ productId: 11, quantity: 2 }], discountCode: 'promo-10' });
    expect(preview.total).toBe(2.7);
  });

  it('previewSale sends no discount code when "none" is selected', async () => {
    const { api, useCases } = setup();
    const cart = useCases.addToCart(Cart.empty(), coffee, 1);

    await useCases.previewSale(cart, 'none');

    expect(api.previewSale).toHaveBeenCalledWith({ lines: [{ productId: 11, quantity: 1 }], discountCode: null });
  });

  it('cart use cases delegate the rules to the domain', () => {
    const { useCases } = setup();

    let cart = useCases.addToCart(Cart.empty(), coffee, 1);
    cart = useCases.changeQuantity(cart, 11, 3);
    expect(cart.subtotal).toBe(4.5);

    cart = useCases.removeFromCart(cart, 11);
    expect(cart.isEmpty).toBe(true);
  });
});
