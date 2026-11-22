import { describe, expect, it, vi, afterEach } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { createCatalogPresenter } from './catalogPresenter.js';
import { Cart } from '../domain/cart.js';
import SalesPanel from './SalesPanel.jsx';
import PosPage from './PosPage.jsx';

const sampleProduct1 = { id: 10, name: 'Café Espresso', code: 'BAR-001', unitPrice: 2.50, quantityAvailable: 20 };
const sampleProduct2 = { id: 20, name: 'Croissant Mantequilla', code: 'BAR-002', unitPrice: 3.00, quantityAvailable: 15 };

afterEach(() => {
  vi.useRealTimers();
});

describe('Odoo POS Agile Workflow & Optimization', () => {
  describe('Catalog Local In-Memory Cache and Instant Match', () => {
    it('caches search results and pre-populates instantly on repeat queries', async () => {
      vi.useFakeTimers();
      const searchMock = vi.fn().mockImplementation(async (term) => {
        if (term === 'Café') return [sampleProduct1];
        if (term === 'Croissant') return [sampleProduct2];
        return [sampleProduct1, sampleProduct2];
      });
      const useCases = { searchProducts: searchMock };
      const presenter = createCatalogPresenter(useCases);

      await presenter.load();
      expect(searchMock).toHaveBeenCalledTimes(1);

      // Search 'Café'
      presenter.edit('Café');
      await vi.advanceTimersByTimeAsync(300);
      expect(searchMock).toHaveBeenCalledWith('Café');
      expect(presenter.read().products).toEqual([sampleProduct1]);
      expect(presenter.getCacheSize()).toBeGreaterThanOrEqual(1);

      // Search 'Croissant'
      presenter.edit('Croissant');
      await vi.advanceTimersByTimeAsync(300);
      expect(searchMock).toHaveBeenCalledWith('Croissant');
      expect(presenter.read().products).toEqual([sampleProduct2]);

      // Return to 'Café' -> instantly populated from cache without waiting for debounce
      presenter.edit('Café');
      expect(presenter.read().products).toEqual([sampleProduct1]);

      presenter.dispose();
    });

    it('identifies exact match by code (barcode) or name with findExactMatch', async () => {
      vi.useFakeTimers();
      const useCases = {
        searchProducts: vi.fn().mockResolvedValue([sampleProduct1, sampleProduct2])
      };
      const presenter = createCatalogPresenter(useCases);
      await presenter.load();

      // Barcode lookup
      const byBarcode = presenter.findExactMatch('BAR-001');
      expect(byBarcode).toEqual(sampleProduct1);

      // Exact name lookup (case-insensitive)
      const byName = presenter.findExactMatch('café espresso');
      expect(byName).toEqual(sampleProduct1);

      // Non-matching query
      const noMatch = presenter.findExactMatch('UNKNOWN-999');
      expect(noMatch).toBeNull();

      presenter.dispose();
    });

    it('clears in-memory cache when clearCache is called', async () => {
      vi.useFakeTimers();
      const useCases = { searchProducts: vi.fn().mockResolvedValue([sampleProduct1]) };
      const presenter = createCatalogPresenter(useCases);
      await presenter.load();
      expect(presenter.getCacheSize()).toBeGreaterThanOrEqual(1);

      presenter.clearCache();
      expect(presenter.getCacheSize()).toBe(0);

      presenter.dispose();
    });
  });

  describe('SalesPanel Reactive Totals and Tendered Cash Calculation', () => {
    it('renders checkout view with tendered input, quick denominations, and calculates positive change', () => {
      const quote = {
        sessionId: 5,
        currency: 'USD',
        total: 15.50,
        methods: [{ id: 1, name: 'Efectivo USD' }],
        lines: [{ productId: 10, quantity: 2, unitPrice: 2.50, net: 5.00, tax: 0.60 }]
      };
      const useCases = {
        pendingSale: () => null,
        activeSession: vi.fn().mockResolvedValue({ id: 5 }),
        quoteSale: vi.fn().mockResolvedValue(quote),
        confirmSale: vi.fn()
      };
      const cart = Cart.empty().addProduct(sampleProduct1, 2);
      const key = JSON.stringify({ lines: cart.toRequestLines(), discountCode: 'none' });
      const mockPresenter = {
        read: () => ({
          key,
          quote,
          method: '1',
          reviewAccepted: false,
          busy: '',
          error: '',
          message: '',
          notice: '',
          outcome: 'idle',
          pending: null
        }),
        subscribe: () => () => {},
        setSale: () => {},
        enterView: () => {},
        selectMethod: () => {},
        acceptReview: () => {},
        dispose: () => {}
      };

      const html = renderToStaticMarkup(
        <SalesPanel
          useCases={useCases}
          cart={cart}
          discountCode="none"
          view="cobro"
          presenter={mockPresenter}
          onLockChange={() => {}}
          onConfirmed={() => {}}
          onContinue={() => {}}
        />
      );

      // Verify the rendered elements include the reactive payment components
      expect(html).toContain('Efectivo exacto');
      expect(html).toContain('tendered-calculator');
      expect(html).toContain('tendered-amount');
      expect(html).toContain('Exacto');
      expect(html).toContain('quick-denominations');
      expect(html).toContain('$20');
      expect(html).toContain('Monto insuficiente. Faltan');
    });
  });

  describe('PosPage Quick Steps and Customer Selection', () => {
    it('renders customer selector and quick quantity increment buttons (+1, +5)', () => {
      const cart = Cart.empty().addProduct(sampleProduct1, 1);
      const useCases = {
        searchProducts: vi.fn().mockResolvedValue([sampleProduct1]),
        listDiscounts: vi.fn().mockReturnValue([
          { code: 'none', name: 'Sin descuento' },
          { code: 'DESC10', name: 'Descuento 10%' }
        ]),
        previewSale: vi.fn().mockReturnValue({
          subtotal: 2.50,
          discount: 0,
          total: 2.50
        }),
        addToCart: (c, p, q) => c.addProduct(p, q),
        changeQuantity: (c, id, q) => c.changeQuantity(id, q),
        removeFromCart: (c, id) => c.removeProduct(id),
        pendingSale: () => null
      };

      const html = renderToStaticMarkup(
        <PosPage
          username="cajero1"
          useCases={useCases}
          initialView="venta"
          initialCart={cart}
          onLogout={() => {}}
        />
      );

      // Verify customer tag & selector
      expect(html).toContain('Consumidor Final');
      expect(html).toContain('customer-tag');
      expect(html).toContain('customer-select');

      // Verify quick step buttons (+1, +5)
      expect(html).toContain('quick-step-btn');
      expect(html).toContain('+1');
      expect(html).toContain('+5');

      // Verify discount dropdown structure
      expect(html).toContain('discount-code');
      expect(html).toContain('Sin descuento');
    });
  });
});
