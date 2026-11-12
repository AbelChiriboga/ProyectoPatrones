import { describe, it, expect, vi } from 'vitest';
import { createUseCases } from './useCases.js';
import { createPendingSale } from '../infrastructure/pendingSale.js';
function setup(api) {
  const values = new Map();
  const storage = { getItem: k => values.get(k) ?? null, setItem: (k,v) => values.set(k,v), removeItem: k => values.delete(k) };
  const pending = createPendingSale(storage, () => 'stable-id');
  const session = { read: () => ({username:'a'}) };
  return { uc: createUseCases({api, session, pending}), pending, storage };
}
describe('durable sale intention', () => {
  it('keeps identity on timeout and reconciles without POST', async () => {
    const api={registerSale:vi.fn().mockRejectedValue(new Error('network')),saleOperation:vi.fn().mockResolvedValue({status:'confirmed'})};
    const x=setup(api);
    await expect(x.uc.confirmSale({lines:[]},1,10)).rejects.toThrow('network');
    expect(createPendingSale(x.storage).read('a').operationId).toBe('stable-id');
    await expect(x.uc.confirmSale({},1,10)).rejects.toThrow('pendiente');
    await x.uc.reconcileSale();expect(api.registerSale).toHaveBeenCalledTimes(1);expect(x.pending.read('a')).toBeNull();
  });
  it('allows a new quote when server rejects a changed total before sending', async () => {
    const x=setup({registerSale:vi.fn().mockRejectedValue(Object.assign(new Error('changed'),{status:409,code:'sale.total_changed'}))});
    await expect(x.uc.confirmSale({},1,10)).rejects.toThrow();expect(x.pending.read('a')).toBeNull();
  });
  it('retains pending for an uncertain response', async () => {
    const x=setup({registerSale:vi.fn().mockResolvedValue({status:'uncertain'})});
    await x.uc.confirmSale({},1,10);expect(x.pending.read('a')).not.toBeNull();
  });
  it('allows correction after definitive validation rejection', async () => {
    const x=setup({registerSale:vi.fn().mockRejectedValue(Object.assign(new Error('invalid'),{status:422}))});
    await expect(x.uc.confirmSale({},1,10)).rejects.toThrow();expect(x.pending.read('a')).toBeNull();
  });
});
