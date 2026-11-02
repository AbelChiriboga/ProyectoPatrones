import { describe, it, expect, vi } from 'vitest';
import { Cart } from '../domain/cart.js';
import { createQuantityEditor } from './quantityEditor.js';
import { createSalesPresenter } from './salesPresenter.js';
const deferred = () => { let resolve, reject; const promise = new Promise((a,b) => {resolve=a;reject=b;}); return {promise,resolve,reject}; };
const sale = { lines: [{ productId: 7, quantity: 1 }], discountCode: 'none' };
const quote = { sessionId: 3, total: 9.66, currency: 'USD', lines: [], methods: [{id:8,name:'Efectivo'}] };
function setup(overrides={}) {
  const api={ pendingSale:()=>null, activeSession:vi.fn().mockResolvedValue({id:3}), quoteSale:vi.fn().mockResolvedValue(quote), confirmSale:vi.fn(), ...overrides };
  const confirmed=vi.fn();const flow=createSalesPresenter(api,confirmed);flow.setSale(sale);return {api,flow,confirmed};
}
describe('CA-03.4-01 quantity editing against domain',()=>{
  it('allows deletion while editing, preserves valid subtotal on invalid commit and clears error on correction',()=>{
    let cart=Cart.empty().addProduct({id:7,name:'A',unitPrice:2,quantityAvailable:5},2);
    const editor=createQuantityEditor(2,n=>{cart=cart.changeQuantity(7,n);});
    expect(editor.edit('')).toEqual({draft:'',error:''});expect(cart.subtotal).toBe(4);
    expect(editor.commit().error).toBeTruthy();expect(cart.lines[0].quantity).toBe(2);expect(cart.subtotal).toBe(4);
    expect(editor.edit('3').error).toBe('');expect(editor.commit().error).toBe('');expect(cart.subtotal).toBe(6);
  });
  it.each(['0','-1','1.5','text','6'])('rejects invalid or over-stock input %s',value=>{
    let cart=Cart.empty().addProduct({id:7,name:'A',unitPrice:2,quantityAvailable:5},2);
    const original=cart;const editor=createQuantityEditor(2,n=>{cart=cart.changeQuantity(7,n);});editor.edit(value);expect(editor.commit(value).error).toBeTruthy();expect(cart).toBe(original);
  });
});
describe('checkout presentation transitions CA-02..07 without browser',()=>{
  it.each([{...sale,lines:[{productId:7,quantity:2}]},{...sale,lines:[{productId:9,quantity:1}]},{...sale,discountCode:'promo-10'}])('invalidates quote for changed sale %j',async changed=>{
    const x=setup();await x.flow.quote();x.flow.acceptReview(true);x.flow.setSale(changed);await x.flow.confirm();expect(x.flow.read().quote).toBeNull();expect(x.api.confirmSale).not.toHaveBeenCalled();
  });
  it('discards a late quote after editing and removes old quote before a failed refresh',async()=>{
    const d=deferred(),x=setup();x.api.quoteSale.mockReturnValueOnce(d.promise);const work=x.flow.quote();x.flow.setSale({...sale,discountCode:'promo-10'});d.resolve(quote);await work;expect(x.flow.read().quote).toBeNull();
    await x.flow.quote();x.flow.acceptReview(true);x.api.quoteSale.mockRejectedValueOnce(new Error('internal trace'));await x.flow.quote();x.flow.acceptReview(true);expect(x.flow.read().quote).toBeNull();expect(x.flow.read().error).not.toContain('internal trace');
  });
  it('two UI confirm actions send once while in flight and confirm only with server evidence',async()=>{
    const d=deferred(),x=setup();await x.flow.quote();x.flow.acceptReview(true);x.api.confirmSale.mockReturnValue(d.promise);const first=x.flow.confirm();await x.flow.confirm();expect(x.api.confirmSale).toHaveBeenCalledTimes(1);expect(x.flow.read().busy).toBeTruthy();
    const record={reference:'Odoo-reference',state:'paid'};d.resolve({status:'confirmed',sale:record});await first;expect(x.flow.read().detail).toBe(record);expect(x.confirmed).toHaveBeenCalledTimes(1);
  });
  it.each(['timeout','uncertain'])('never retries or announces paid after %s; reconciles with existing use case',async mode=>{
    let pending=null;const x=setup({pendingSale:()=>pending,reconcileSale:vi.fn().mockResolvedValue({status:'uncertain'})});await x.flow.quote();x.flow.acceptReview(true);
    x.api.confirmSale.mockImplementation(async()=>{pending={operationId:'stable'};if(mode==='timeout')throw new Error('timeout');return {status:'uncertain'};});
    await x.flow.confirm();await x.flow.confirm();expect(x.api.confirmSale).toHaveBeenCalledTimes(1);expect(x.flow.read().message+x.flow.read().error).not.toContain('paid');expect(x.flow.read().pending.operationId).toBe('stable');expect(x.confirmed).not.toHaveBeenCalled();await x.flow.reconcile();expect(x.api.reconcileSale).toHaveBeenCalledTimes(1);
  });
  it.each(['session.not_open','products.not_found'])('provides actionable rejection for %s without success',async code=>{
    const x=setup();await x.flow.quote();x.flow.acceptReview(true);x.api.confirmSale.mockRejectedValue({status:422,code});await x.flow.confirm();expect(x.flow.read().error).toContain('cotizar');expect(x.flow.read().quote).toBeNull();expect(x.confirmed).not.toHaveBeenCalled();
  });
  it('history and detail preserve API reference, amounts and state through navigation actions',async()=>{
    const record={id:42,reference:'Runtime sale',total:17.21,state:'done',currency:'EUR'};
    const x=setup({salesHistory:vi.fn().mockResolvedValue([record]),saleDetail:vi.fn().mockResolvedValue(record)});
    await x.flow.history();expect(x.flow.read().history).toEqual([record]);await x.flow.detail(42);expect(x.flow.read().detail).toBe(record);expect(x.api.saleDetail).toHaveBeenCalledWith(42);
    x.api.salesHistory.mockResolvedValue([]);await x.flow.history(42);expect(x.flow.read().historyLoaded).toBe(true);expect(x.flow.read().detail).toBeNull();expect(x.api.salesHistory).toHaveBeenLastCalledWith(42);
  });
});
