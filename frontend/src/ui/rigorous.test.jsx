import { describe, it, expect, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import PosPage from './PosPage.jsx';
import { createViewNavigation } from './viewNavigation.js';
import { createSalesPresenter } from './salesPresenter.js';
import { Cart } from '../domain/cart.js';
import { createQuantityEditor, commitQuantityControl } from './quantityEditor.js';

describe('DEF-01 lexical rejection before changing the controlled value', () => {
  it.each(['abc','2a','@#!','-2','1.5','1e2','+3',' 3 ','0x10','٣','999999999999999999999999'])('rejects keyboard/paste value %s without truncation', value => {
    const change=vi.fn(); const editor=createQuantityEditor(2,change);
    const state=editor.edit(value);
    expect(state.draft).toBe('2'); expect(state.error).not.toBe('');
    editor.commit(); expect(change).not.toHaveBeenCalled();
  });
  it('checks the actual control value again on commit (programmatic mutation)', () => {
    const change=vi.fn(); const editor=createQuantityEditor(2,change);
    expect(editor.commit('1e2').error).not.toBe(''); expect(change).not.toHaveBeenCalled();
  });
  it('domain also rejects numbers that cannot be represented exactly', () => {
    const cart=Cart.empty().addProduct({id:7,name:'A',unitPrice:2},2);
    expect(()=>cart.changeQuantity(7,Number.MAX_SAFE_INTEGER+1)).toThrow();
  });
});
describe('DEF-02/03 exclusive view rendering',()=>{
  const render = initialView => renderToStaticMarkup(<PosPage initialView={initialView} useCases={{pendingSale:()=>null}} username="a" onLogout={()=>{}} />);
  it('history contains its action but excludes checkout and catalog controls',()=>{
    const html=render('historial'); expect(html).toContain('Ver historial'); expect(html).not.toContain('Buscar productos'); expect(html).not.toContain('Consultar total con impuestos'); expect(html).not.toContain('No hay operaciones pendientes');
  });
  it('pending view excludes history and checkout controls',()=>{
    const html=render('operaciones'); expect(html).toContain('No hay operaciones pendientes'); expect(html).not.toContain('Ver historial'); expect(html).not.toContain('Buscar productos'); expect(html).not.toContain('Consultar total con impuestos');
  });
});


describe('Enter/blur and recovery share the actual control validation',()=>{
  it.each(['blur','keydown'])('revalidates %s even without onChange',type=>{
    const change=vi.fn(),editor=createQuantityEditor(2,change),preventDefault=vi.fn();
    const event={type,key:'Enter',preventDefault,currentTarget:{value:'4x'}};
    expect(commitQuantityControl(editor,event).error).not.toBe('');expect(change).not.toHaveBeenCalled();
    editor.edit('4');event.currentTarget.value='4';expect(commitQuantityControl(editor,event).error).toBe('');
    commitQuantityControl(editor,event);expect(change).toHaveBeenCalledTimes(1);expect(change).toHaveBeenCalledWith(4);
  });
  it('ignores non-Enter keys until editing is committed',()=>{
    const change=vi.fn(),editor=createQuantityEditor(2,change);editor.edit('3');
    expect(commitQuantityControl(editor,{type:'keydown',key:'ArrowRight'})).toBeNull();expect(change).not.toHaveBeenCalled();
  });
  it('allows empty draft, rejects zero/empty on leaving, then recovers without losing valid data',()=>{
    let cart=Cart.empty().addProduct({id:7,name:'A',unitPrice:2},2);const before=cart;
    const editor=createQuantityEditor(2,n=>{cart=cart.changeQuantity(7,n);});
    expect(editor.edit('')).toEqual({draft:'',error:''});editor.commit('');expect(cart).toBe(before);
    editor.edit('0');expect(editor.commit('0').error).not.toBe('');expect(cart).toBe(before);
    editor.edit('5');expect(editor.commit('5').error).toBe('');expect(cart.subtotal).toBe(10);
  });
});
describe('hash navigation without browser or cart/checkout reset',()=>{
  it('selects three views, supports back and returns to the same confirmed cart and quote',async()=>{
    let hash='#venta';const listeners=new Set();
    const port={readHash:()=>hash,onHashChange:fn=>{listeners.add(fn);return()=>listeners.delete(fn);},writeHash:value=>{hash=value;listeners.forEach(fn=>fn());}};
    const nav=createViewNavigation(port);const changed=vi.fn();const unsubscribe=nav.subscribe(changed);
    const cart=Cart.empty().addProduct({id:7,name:'A',unitPrice:2},2);
    const api={pendingSale:()=>null,activeSession:vi.fn().mockResolvedValue({id:3}),quoteSale:vi.fn().mockResolvedValue({sessionId:3,total:4,currency:'USD',lines:[],methods:[{id:8,name:'Efectivo'}]})};
    const checkout=createSalesPresenter(api);checkout.setSale({lines:cart.toRequestLines(),discountCode:'none'});await checkout.quote();const verified=checkout.read().quote;
    expect(nav.read()).toBe('venta');nav.select('historial');expect(nav.read()).toBe('historial');nav.select('operaciones');expect(nav.read()).toBe('operaciones');port.writeHash('#historial');expect(nav.read()).toBe('historial');nav.select('venta');expect(nav.read()).toBe('venta');
    expect(changed).toHaveBeenCalledTimes(4);expect(cart.lines[0].quantity).toBe(2);expect(cart.subtotal).toBe(4);expect(checkout.read().quote).toBe(verified);expect(api.quoteSale).toHaveBeenCalledTimes(1);
    unsubscribe();port.writeHash('#historial');expect(changed).toHaveBeenCalledTimes(4);
  });
  it('unknown view falls back safely to sale',()=>{
    const nav=createViewNavigation({readHash:()=> '#unknown'});expect(nav.read()).toBe('venta');
  });
});


describe('review and communication recovery',()=>{
  it('preserves API quote fields, recovers after connection failure and does not mix browsed detail with operation result',async()=>{
    const dto={sessionId:4,total:12.37,currency:'USD',lines:[{productId:8,quantity:2,net:11,tax:1.37,discountPercent:5}],methods:[{id:9,name:'Efectivo de caja'}]};
    const record={id:91,reference:'Historical order',state:'paid'};
    const api={pendingSale:()=>null,activeSession:vi.fn().mockResolvedValue({id:4}),quoteSale:vi.fn().mockRejectedValueOnce(new Error('network')).mockResolvedValue(dto),saleDetail:vi.fn().mockResolvedValue(record),confirmSale:vi.fn()};
    const flow=createSalesPresenter(api);flow.setSale({lines:[{productId:8,quantity:2}],discountCode:'promo'});
    await flow.quote();expect(flow.read().error).not.toBe('');expect(flow.read().quote).toBeNull();expect(flow.read().busy).toBe('');
    await flow.quote();expect(flow.read().error).toBe('');expect(flow.read().quote).toBe(dto);expect(flow.read().method).toBe('9');
    await flow.detail(91);expect(flow.read().detail).toBe(record);expect(flow.read().resultSale).toBeNull();expect(api.confirmSale).not.toHaveBeenCalled();
  });
});


describe('invalid draft cannot contaminate a verified sale payload',()=>{
  it('preserves cart, subtotal and quote and sends only the last confirmed quantity',async()=>{
    let cart=Cart.empty().addProduct({id:7,name:'A',unitPrice:2},2);const original=cart;
    const api={pendingSale:()=>null,activeSession:vi.fn().mockResolvedValue({id:3}),quoteSale:vi.fn().mockResolvedValue({sessionId:3,total:4,lines:[],methods:[{id:8,name:'Efectivo'}]}),confirmSale:vi.fn().mockResolvedValue({status:'uncertain'})};
    const flow=createSalesPresenter(api);const source=()=>({lines:cart.toRequestLines(),discountCode:'none'});flow.setSale(source());await flow.quote();const quote=flow.read().quote;
    const editor=createQuantityEditor(2,n=>{cart=cart.changeQuantity(7,n);flow.setSale(source());});editor.edit('2e3');editor.commit('2e3');
    expect(cart).toBe(original);expect(cart.subtotal).toBe(4);expect(flow.read().quote).toBe(quote);await flow.confirm();
    expect(api.confirmSale).toHaveBeenCalledWith({sessionId:3,lines:[{productId:7,quantity:2}],discountCode:'none'},8,4);
  });
});
