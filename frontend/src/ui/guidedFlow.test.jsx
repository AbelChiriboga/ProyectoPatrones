import { describe, expect, it, vi, afterEach } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { parseCatalogSearch } from '../application/catalogSearch.js';
import { createUseCases } from '../application/useCases.js';
import { Cart } from '../domain/cart.js';
import { createCatalogPresenter } from './catalogPresenter.js';
import { createQuantityEditor, commitQuantityControl } from './quantityEditor.js';
import { createSalesPresenter } from './salesPresenter.js';
import { createViewNavigation } from './viewNavigation.js';
import CatalogPanel from './CatalogPanel.jsx';
import QuantityInput from './QuantityInput.jsx';
import { HistoryView, QuoteReview } from './SalesViews.jsx';
import { quoteSummary, orderStateLabel } from './salesViewModel.js';

const deferred = () => { let resolve, reject; const promise = new Promise((a,b) => { resolve=a; reject=b; }); return {promise,resolve,reject}; };
const product = {id:7,name:'Café americano',code:'CAFE-001',unitPrice:2,quantityAvailable:3};
const noop = () => {};
afterEach(() => vi.useRealTimers());

describe('catalog query policy and actual request boundaries',()=>{
  it.each(['Café americano','Sánduche de pollo','CAFE-001','  té con limón  ','Galleta (avena)','Café & té','100% cacao',"Pan d'ajo",'', '   '])('accepts legitimate query %j',async term=>{
    const api={searchProducts:vi.fn().mockResolvedValue([])};
    const useCases=createUseCases({api,session:{}});
    await useCases.searchProducts(term);expect(api.searchProducts).toHaveBeenCalledExactlyOnceWith(term.trim());
  });
  it.each(['café\tCAFE','café\n','a\u200B','a\u2028','a\u007f','%%%%'])('rejects non-printing/noise paste %j before HTTP',term=>{
    const api={searchProducts:vi.fn()},useCases=createUseCases({api,session:{}});
    expect(()=>useCases.searchProducts(term)).toThrow();expect(api.searchProducts).not.toHaveBeenCalled();
  });
  it('uses the existing 100 character query limit without silently truncating',()=>{
    expect(parseCatalogSearch('a'.repeat(100))).toHaveLength(100);
    expect(()=>parseCatalogSearch('a'.repeat(101))).toThrow('100');
  });
  it('retains the preceding valid input, cancels rejected requests and clears the error in the correction interaction',async()=>{
    vi.useFakeTimers();const cases={searchProducts:vi.fn().mockResolvedValue([product])};
    const model=createCatalogPresenter(cases);await model.load();
    model.edit('CAFE-001');model.edit('x'.repeat(101));
    expect(model.read().term).toBe('CAFE-001');expect(model.read().products).toEqual([product]);expect(model.read().inputError).toContain('100');
    await vi.advanceTimersByTimeAsync(500);expect(cases.searchProducts).toHaveBeenCalledTimes(1);
    model.edit('Café');expect(model.read().inputError).toBe('');await vi.advanceTimersByTimeAsync(300);
    expect(cases.searchProducts).toHaveBeenLastCalledWith('Café');expect(model.read().status).toBe('ready');model.dispose();
  });
  it('separates empty catalog, valid no-match, rejected query and connection error; retries without erasing the term',async()=>{
    vi.useFakeTimers();const cases={searchProducts:vi.fn().mockResolvedValue([])};const model=createCatalogPresenter(cases);
    const html=()=>renderToStaticMarkup(<CatalogPanel state={model.read()} onSearch={noop} onRetry={noop} onAdd={noop} />);
    await model.load();expect(html()).toContain('No hay productos disponibles');
    model.edit('Jugo');await vi.advanceTimersByTimeAsync(300);expect(html()).toContain('No se encontraron productos para «Jugo»');
    model.edit('!!!');expect(html()).toContain('search-error');expect(html()).not.toContain('No se encontraron productos');
    model.edit('Café');cases.searchProducts.mockRejectedValueOnce(new Error('private stack'));
    await vi.advanceTimersByTimeAsync(300);expect(model.read().status).toBe('error');expect(model.read().term).toBe('Café');expect(html()).toContain('Reintentar catálogo');expect(html()).not.toContain('private stack');expect(html()).not.toContain('No se encontraron productos');
    await model.load();expect(model.read().networkError).toBe('');expect(model.read().status).toBe('ready');model.dispose();
  });
  it('ignores late catalog responses after input replacement and disposes queued work',async()=>{
    vi.useFakeTimers();const old=deferred();const cases={searchProducts:vi.fn().mockReturnValueOnce(old.promise).mockResolvedValue([])};const model=createCatalogPresenter(cases);
    const loading=model.load();model.edit('Jugo');await vi.advanceTimersByTimeAsync(300);old.resolve([product]);await loading;
    expect(model.read().searchedTerm).toBe('Jugo');expect(model.read().products).toEqual([]);
    model.edit('Café');model.dispose();await vi.advanceTimersByTimeAsync(500);expect(cases.searchProducts).toHaveBeenCalledTimes(2);
  });
});

describe('quantity correction, composition and explicit step actions',()=>{
  it('supports gradual typing, replacing 3 with 5 and immediate recovery of the error without reload',()=>{
    let cart=Cart.empty().addProduct({...product,quantityAvailable:null},1);
    const model=createQuantityEditor(1,n=>{cart=cart.changeQuantity(7,n);});
    model.edit('1');model.edit('10');expect(cart.lines[0].quantity).toBe(1);model.commit('10');expect(cart.lines[0].quantity).toBe(10);
    model.edit('');expect(model.read().error).toBe('');model.commit('');expect(model.read().restored).toBe(true);expect(model.read().draft).toBe('10');model.edit('x');expect(model.read().restored).toBeUndefined();model.commit('10');expect(model.read().error).toBe('');model.edit('3');model.commit('3');expect(cart.lines[0].quantity).toBe(3);
    model.edit('!');expect(model.read().error).not.toBe('');model.edit('5');expect(model.read().error).toBe('');model.commit('5');expect(cart.lines[0].quantity).toBe(5);
    model.edit('e');model.commit('5');expect(model.read().error).toBe('');expect(cart.lines[0].quantity).toBe(5);
  });
  it('step actions respect one, real stock and erase corrected errors; no invented maximum 99',()=>{
    let cart=Cart.empty().addProduct(product,2);const editor=createQuantityEditor(2,n=>{cart=cart.changeQuantity(7,n);});
    editor.step(1);expect(cart.lines[0].quantity).toBe(3);editor.step(1);expect(editor.read().error).toContain('3');expect(cart.lines[0].quantity).toBe(3);
    editor.step(-1);expect(editor.read().error).toBe('');expect(cart.lines[0].quantity).toBe(2);editor.step(-1);editor.step(-1);expect(cart.lines[0].quantity).toBe(1);expect(editor.read().error).not.toBe('');
    let wide=Cart.empty().addProduct({...product,quantityAvailable:null},100);const unrestricted=createQuantityEditor(100,n=>{wide=wide.changeQuantity(7,n);});unrestricted.step(1);expect(wide.lines[0].quantity).toBe(101);
  });
  it('does not commit IME Enter, and validates the composed raw value on completion',()=>{
    const change=vi.fn(),editor=createQuantityEditor(1,change);
    expect(commitQuantityControl(editor,{type:'keydown',key:'Enter',nativeEvent:{isComposing:true},currentTarget:{value:'二'}})).toBeNull();expect(change).not.toHaveBeenCalled();
    expect(editor.edit('二').draft).toBe('1');expect(editor.read().error).not.toBe('');
    editor.edit('2');commitQuantityControl(editor,{type:'blur',currentTarget:{value:'2'}});expect(change).toHaveBeenCalledExactlyOnceWith(2);
  });
  it('renders accessible decrement/increment controls disabled at the existing boundaries',()=>{
    const html=renderToStaticMarkup(<QuantityInput line={{...product,productId:7,quantity:1,quantityAvailable:1}} onCommit={noop} />);
    expect(html).toMatch(/aria-label="Disminuir cantidad de Café americano" disabled/);
    expect(html).toMatch(/aria-label="Aumentar cantidad de Café americano" disabled/);
    expect(html).toContain('aria-label="Cantidad de Café americano"');
  });
});

describe('guided views, auto-history and verified review',()=>{
  it('loads on history entry once even on repeated effect; exposes empty/error/retry and reloads on reentry',async()=>{
    const api={pendingSale:()=>null,salesHistoryPage:vi.fn().mockRejectedValueOnce(new Error('network')).mockResolvedValue({items:[],hasMore:false,nextBeforeId:null})};
    const flow=createSalesPresenter(api);const first=flow.enterView('historial');flow.enterView('historial');
    expect(flow.read().historyStatus).toBe('loading');await first;expect(api.salesHistoryPage).toHaveBeenCalledTimes(1);expect(flow.read().historyStatus).toBe('error');
    const failed=renderToStaticMarkup(<HistoryView state={flow.read()} onRetry={noop} onDetail={noop} onNext={noop} />);
    expect(failed).toContain('Reintentar historial');expect(failed).not.toContain('No hay ventas en esta página');
    await flow.history();expect(flow.read().historyStatus).toBe('ready');expect(flow.read().historyError).toBe('');
    const empty=renderToStaticMarkup(<HistoryView state={flow.read()} onRetry={noop} onDetail={noop} onNext={noop} />);
    expect(empty).toContain('No hay ventas en esta página');expect(empty).not.toContain('Ver historial');
    await flow.enterView('venta');await flow.enterView('historial');expect(api.salesHistoryPage).toHaveBeenCalledTimes(3);
  });
  it('can load history during another in-flight action and recover from synchronous read failure',async()=>{
    const slow=deferred();const api={pendingSale:()=>null,activeSession:vi.fn().mockReturnValue(slow.promise),salesHistoryPage:vi.fn().mockImplementationOnce(()=>{throw new Error('offline');}).mockResolvedValue({items:[],hasMore:false,nextBeforeId:null}),quoteSale:vi.fn().mockResolvedValue({methods:[],lines:[]})};
    const flow=createSalesPresenter(api);flow.setSale({lines:[{productId:7,quantity:1}],discountCode:'none'});
    const quote=flow.enterView('cobro');await flow.enterView('historial');expect(flow.read().historyStatus).toBe('error');await flow.history();expect(flow.read().historyStatus).toBe('ready');
    slow.resolve({id:3});await quote;
  });
  it('navigates Venta–Cobro–Venta, preserves cart, invalidates quote/review on edits and gates duplicate confirmation',async()=>{
    let hash='#venta';const nav=createViewNavigation({readHash:()=>hash,writeHash:value=>{hash=value;}});
    const d=deferred();let pending=null;
    const quote={sessionId:3,currency:'USD',total:4,lines:[],methods:[{id:8,name:'Efectivo'}]};
    const api={pendingSale:()=>pending,activeSession:vi.fn().mockResolvedValue({id:3}),quoteSale:vi.fn().mockResolvedValue(quote),confirmSale:vi.fn().mockImplementation(()=>{pending={operationId:'same-id'};return d.promise;}),reconcileSale:vi.fn().mockResolvedValue({status:'uncertain'})};
    const flow=createSalesPresenter(api);let cart=Cart.empty().addProduct({...product,quantityAvailable:null},2);const sync=()=>flow.setSale({lines:cart.toRequestLines(),discountCode:'none'});sync();
    nav.select('cobro');await flow.enterView(nav.read());expect(flow.read().quote).toBe(quote);
    await flow.confirm();expect(api.confirmSale).not.toHaveBeenCalled();flow.acceptReview(true);
    nav.select('venta');await flow.enterView(nav.read());expect(cart.lines[0].quantity).toBe(2);expect(flow.read().quote).toBe(quote);
    cart=cart.changeQuantity(7,3);sync();expect(flow.read().quote).toBeNull();expect(flow.read().reviewAccepted).toBe(false);
    nav.select('cobro');await flow.enterView(nav.read());expect(api.quoteSale).toHaveBeenCalledTimes(2);flow.acceptReview(true);
    const first=flow.confirm();await flow.confirm();expect(api.confirmSale).toHaveBeenCalledTimes(1);
    d.resolve({status:'uncertain'});await first;expect(flow.read().outcome).toBe('uncertain');expect(flow.read().message).not.toContain('paid');await flow.enterView('venta');await flow.enterView('cobro');expect(api.confirmSale).toHaveBeenCalledTimes(1);expect(api.quoteSale).toHaveBeenCalledTimes(2);
    await flow.reconcile();expect(api.reconcileSale).toHaveBeenCalledTimes(1);expect(flow.read().pending.operationId).toBe('same-id');
  });
  it('displays API amounts and understandable Odoo states without inventing a tax rate',async()=>{
    const quote={sessionId:3,currency:'USD',total:12.37,lines:[{productId:7,quantity:2,unitPrice:6,net:11,tax:1.37,discountPercent:8.3333}],methods:[{id:8,name:'Efectivo'}]};
    expect(quoteSummary(quote)).toEqual({subtotal:12,discount:1,tax:1.37,total:12.37});
    const review=renderToStaticMarkup(<QuoteReview quote={quote} cart={Cart.empty().addProduct(product,2)} />);
    expect(review).toContain('1,37 USD');expect(review).toContain('12,37 USD');expect(review).toContain('Café americano');
    const order={id:98,reference:'Caja-real-98',state:'paid',currency:'USD',total:12.37};
    const api={pendingSale:()=>null,salesHistoryPage:vi.fn().mockResolvedValue({items:[order],hasMore:false,nextBeforeId:null}),saleDetail:vi.fn().mockResolvedValue({...order,lines:quote.lines,methods:quote.methods,tax:1.37,paid:12.37})};
    const flow=createSalesPresenter(api);await flow.enterView('historial');await flow.detail(98);
    const history=renderToStaticMarkup(<HistoryView state={flow.read()} onRetry={noop} onDetail={noop} onNext={noop} />);
    expect(history).toContain('Caja-real-98');expect(history).toContain('Pagada');expect(history).toContain('(paid)');expect(history).not.toContain('Ver historial');expect(orderStateLabel('done')).toBe('Contabilizada');
  });
});
