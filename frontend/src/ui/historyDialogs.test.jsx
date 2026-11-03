import { describe, expect, it, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { createSalesPresenter } from './salesPresenter.js';
import { createRemovalPresenter } from './removalPresenter.js';
import { activateDialog, containDialogFocus, cancelDialog, ignoreDialogBackdrop } from './dialogBehavior.js';
import { HistoryView, SaleDetailDialog } from './SalesViews.jsx';
import RemoveProductDialog from './RemoveProductDialog.jsx';
import PosPage from './PosPage.jsx';
import { Cart } from '../domain/cart.js';
import { createUseCases } from '../application/useCases.js';
import { createHttpPosApi } from '../infrastructure/httpPosApi.js';

const noop=()=>{};
const deferred=()=>{let resolve,reject;const promise=new Promise((a,b)=>{resolve=a;reject=b;});return {promise,resolve,reject};};
const item=(id,reference=`Caja ${id}`)=>({id,reference,dateUtc:'2026-11-02T03:00:00Z',total:9.66,currency:'USD',state:'paid'});
const page=(items,hasMore=false)=>({items,hasMore,nextBeforeId:hasMore?items.at(-1).id:null});
const detail=id=>({...item(id),lines:[{productId:7,quantity:2,unitPrice:4.2,net:8.4,tax:1.26,discountPercent:0}],methods:[{id:8,name:'Efectivo'}],tax:1.26,paid:9.66});
function setup(overrides={}) {
  const cases={pendingSale:()=>null,salesHistoryPage:vi.fn().mockResolvedValue(page([item(50)])),saleDetail:vi.fn().mockImplementation(async id=>detail(id)),...overrides};
  return {cases,model:createSalesPresenter(cases)};
}

describe('remote history, search and cursor navigation',()=>{
  it('auto-loads a bounded page, searches remotely and retains only cursor history for previous/next',async()=>{
    const x=setup({salesHistoryPage:vi.fn().mockResolvedValueOnce(page([item(50),item(49)],true)).mockResolvedValueOnce(page([item(48)])).mockResolvedValueOnce(page([item(50),item(49)],true)).mockResolvedValueOnce(page([item(21,'Caja buscada')]))});
    await x.model.enterView('historial');expect(x.cases.salesHistoryPage).toHaveBeenLastCalledWith({search:'',beforeId:0,limit:20});
    await x.model.nextHistory();expect(x.cases.salesHistoryPage).toHaveBeenLastCalledWith({search:'',beforeId:49,limit:20});expect(x.model.read().historyIndex).toBe(1);expect(x.model.read().history.map(row=>row.id)).toEqual([48]);
    await x.model.previousHistory();expect(x.cases.salesHistoryPage).toHaveBeenLastCalledWith({search:'',beforeId:0,limit:20});expect(x.model.read().historyIndex).toBe(0);
    x.model.editHistorySearch(' Caja buscada ');await x.model.searchHistory();expect(x.cases.salesHistoryPage).toHaveBeenLastCalledWith({search:'Caja buscada',beforeId:0,limit:20});expect(x.model.read().historyCursors).toEqual([0]);expect(x.model.read().historyQuery).toBe('Caja buscada');
    await x.model.nextHistory();expect(x.cases.salesHistoryPage).toHaveBeenCalledTimes(4);
  });
  it('keeps applied query/cursor for pagination and retry, not an invalid search',async()=>{
    const x=setup({salesHistoryPage:vi.fn().mockResolvedValueOnce(page([item(50)],true)).mockRejectedValueOnce(new Error('network')).mockResolvedValue(page([item(49)]))});
    x.model.editHistorySearch('Caja');await x.model.searchHistory();await x.model.nextHistory();expect(x.model.read().historyStatus).toBe('error');
    await x.model.retryHistory();expect(x.cases.salesHistoryPage).toHaveBeenLastCalledWith({search:'Caja',beforeId:50,limit:20});
    x.model.editHistorySearch('bad\n');expect(x.model.read().historyTerm).toBe('Caja');await x.model.searchHistory();expect(x.cases.salesHistoryPage).toHaveBeenCalledTimes(3);
    x.model.editHistorySearch('fixed');expect(x.model.read().historySearchError).toBe('');
  });
  it('ignores stale search pages and resets paging for a new query',async()=>{
    const old=deferred();const x=setup({salesHistoryPage:vi.fn().mockReturnValueOnce(old.promise).mockResolvedValue(page([item(7)]))});
    const first=x.model.enterView('historial');x.model.editHistorySearch('new');await x.model.searchHistory();old.resolve(page([item(99)],true));await first;
    expect(x.model.read().history[0].id).toBe(7);expect(x.model.read().historyQuery).toBe('new');expect(x.model.read().historyHasMore).toBe(false);
  });
  it('rejects malformed cursor pages rather than looping or downloading everything',async()=>{
    const x=setup({salesHistoryPage:vi.fn().mockResolvedValue({items:[item(10)],hasMore:true,nextBeforeId:null})});await x.model.enterView('historial');expect(x.model.read().historyStatus).toBe('error');await x.model.nextHistory();expect(x.cases.salesHistoryPage).toHaveBeenCalledTimes(1);
  });
  it('renders structured date/reference/amount/state, with bounded navigation and no inline detail',async()=>{
    const x=setup();await x.model.enterView('historial');await x.model.detail(50);
    const html=renderToStaticMarkup(<HistoryView state={x.model.read()} onRetry={noop} onDetail={noop} onNext={noop} onCloseDetail={noop} />);
    expect(html).toContain('<table');expect(html).toContain('Fecha (UTC)');expect(html).toContain('Caja 50');expect(html).toContain('9,66 USD');expect(html).toContain('Pagada');expect(html).toContain('<dialog');expect(html).toContain('Cerrar detalle');expect(html).not.toContain('Ver historial');
    const list=html.slice(0,html.indexOf('<dialog'));expect(list).not.toContain('aria-label="Detalle Caja 50"');expect(list).not.toContain('Producto 7');
  });
  it('validates before HTTP and preserves the legacy endpoint alongside the additive one',async()=>{
    const fetchFn=vi.fn().mockResolvedValue({ok:true,status:200,json:async()=>page([])});
    const api=createHttpPosApi({baseUrl:'http://api.test',getToken:()=> 'synthetic',fetchFn});const cases=createUseCases({api,session:{}});
    await cases.salesHistoryPage({search:'Caja & 002',beforeId:15,limit:20});expect(fetchFn.mock.calls[0][0]).toBe('http://api.test/api/sales/history?search=Caja%20%26%20002&beforeId=15&limit=20');
    expect(()=>cases.salesHistoryPage({search:'x'.repeat(101)})).toThrow();expect(()=>cases.salesHistoryPage({beforeId:-1})).toThrow();expect(fetchFn).toHaveBeenCalledTimes(1);
    await api.salesHistory(15);expect(fetchFn.mock.calls[1][0]).toBe('http://api.test/api/sales?beforeId=15&limit=20');
  });
});

describe('detail modal lifecycle and stale responses',()=>{
  it('opens immediately with loading and closes safely while response is in flight',async()=>{
    const d=deferred();const x=setup({saleDetail:vi.fn().mockReturnValue(d.promise)});await x.model.enterView('historial');const rows=x.model.read().history;
    const request=x.model.detail(50);expect(x.model.read().detailOpen).toBe(true);expect(x.model.read().detailStatus).toBe('loading');x.model.closeDetail();d.resolve(detail(50));await request;
    expect(x.model.read().detailOpen).toBe(false);expect(x.model.read().detail).toBeNull();expect(x.model.read().history).toBe(rows);
  });
  it('never displays another order from a late response or mismatched server identifier',async()=>{
    const a=deferred(),b=deferred();const x=setup({saleDetail:vi.fn().mockReturnValueOnce(a.promise).mockReturnValueOnce(b.promise).mockResolvedValue(detail(99))});
    const first=x.model.detail(1);const second=x.model.detail(2);b.resolve(detail(2));await second;a.resolve(detail(1));await first;expect(x.model.read().detail.id).toBe(2);
    await x.model.detail(3);expect(x.model.read().detail).toBeNull();expect(x.model.read().detailStatus).toBe('error');
  });
  it('keeps error and retry in the dialog; navigation out closes it without changing the cart or charging',async()=>{
    const x=setup({saleDetail:vi.fn().mockRejectedValueOnce(new Error('private trace')).mockResolvedValue(detail(50)),confirmSale:vi.fn()});
    await x.model.detail(50);expect(x.model.read().detailStatus).toBe('error');
    const html=renderToStaticMarkup(<SaleDetailDialog state={x.model.read()} onClose={noop} onRetry={noop} />);expect(html).toContain('Reintentar detalle');expect(html).not.toContain('private trace');
    await x.model.detail(50);expect(x.model.read().detailStatus).toBe('ready');await x.model.enterView('venta');expect(x.model.read().detailOpen).toBe(false);expect(x.cases.confirmSale).not.toHaveBeenCalled();
  });
});

function dialogPort() {
  const doc={activeElement:null,body:{style:{overflow:'auto'}},defaultView:{scrollX:4,scrollY:640,scrollTo:vi.fn()},querySelector:()=>null};
  const control=name=>({name,tabIndex:0,isConnected:true,hidden:false,disabled:false,closest:()=>null,focus:vi.fn(function(){doc.activeElement=this;})});
  const opener=control('opener'),cancel=control('cancel'),confirm=control('confirm'),disabled={...control('disabled'),disabled:true};doc.activeElement=opener;
  const dialog={ownerDocument:doc,open:false,showModal:vi.fn(function(){this.open=true;}),close:vi.fn(function(){this.open=false;}),querySelector:()=>cancel,querySelectorAll:()=>[cancel,confirm,disabled],focus:vi.fn(function(){doc.activeElement=this;})};
  return {dialog,doc,opener,cancel,confirm};
}
describe('reusable native dialog focus adapter (no browser)',()=>{
  it('opens modally, focuses the safe action and restores opener/scroll exactly on explicit close',()=>{
    const x=dialogPort();const cleanup=activateDialog(x.dialog);expect(x.dialog.showModal).toHaveBeenCalledTimes(1);expect(x.doc.activeElement).toBe(x.cancel);expect(x.doc.body.style.overflow).toBe('hidden');
    cleanup();expect(x.dialog.close).toHaveBeenCalledTimes(1);expect(x.opener.focus).toHaveBeenCalledWith({preventScroll:true});expect(x.doc.activeElement).toBe(x.opener);expect(x.doc.body.style.overflow).toBe('auto');expect(x.doc.defaultView.scrollTo).toHaveBeenCalledWith(4,640);
  });
  it('wraps Tab and Shift+Tab inside the dialog, excluding disabled controls',()=>{
    const x=dialogPort();activateDialog(x.dialog);const event={key:'Tab',shiftKey:true,preventDefault:vi.fn()};containDialogFocus(event,x.dialog);expect(x.doc.activeElement).toBe(x.confirm);expect(event.preventDefault).toHaveBeenCalled();
    containDialogFocus({...event,shiftKey:false},x.dialog);expect(x.doc.activeElement).toBe(x.cancel);
    x.doc.activeElement=x.opener;containDialogFocus({...event,shiftKey:false},x.dialog);expect(x.doc.activeElement).toBe(x.cancel);
  });
  it('uses a safe heading if the removed product opener no longer exists and traps focus without controls',()=>{
    const x=dialogPort();const cleanup=activateDialog(x.dialog);
    x.opener.isConnected=false;x.doc.querySelector=()=>x.confirm;
    x.dialog.querySelectorAll=()=>[];const event={key:'Tab',preventDefault:vi.fn()};
    containDialogFocus(event,x.dialog);expect(event.preventDefault).toHaveBeenCalled();expect(x.doc.activeElement).toBe(x.dialog);
    cleanup();expect(x.opener.focus).not.toHaveBeenCalled();expect(x.confirm.focus).toHaveBeenCalledWith({preventScroll:true});
  });
  it('ignores backdrop clicks and native Escape can only dismiss, never confirm',()=>{
    const x=dialogPort(),dismiss=vi.fn(),confirm=vi.fn();const event={currentTarget:x.dialog,target:x.dialog,preventDefault:vi.fn()};
    ignoreDialogBackdrop(event);expect(event.preventDefault).toHaveBeenCalledTimes(1);expect(dismiss).not.toHaveBeenCalled();
    containDialogFocus({key:'Escape'},x.dialog);expect(confirm).not.toHaveBeenCalled();cancelDialog(event,dismiss);expect(dismiss).toHaveBeenCalledTimes(1);expect(confirm).not.toHaveBeenCalled();
  });
  it('has accessible title/description, visible safe actions and no window.confirm fallback',()=>{
    const html=renderToStaticMarkup(<RemoveProductDialog state={{target:{productId:7,name:'Café'},busy:false,error:''}} onCancel={noop} onConfirm={noop} />);
    expect(html).toContain('role="dialog"');expect(html).toContain('aria-modal="true"');expect(html).toContain('aria-labelledby=');expect(html).toContain('aria-describedby=');expect(html).toContain('¿Quitar producto del carrito?');expect(html).toContain('data-dialog-initial-focus');expect(html).toContain('Cancelar');expect(html).toContain('Sí, quitar producto');
  });
});

describe('remove confirmation delegates only once to the existing cart use case',()=>{
  function setupRemoval() {
    let cart=Cart.empty().addProduct({id:7,name:'Café',unitPrice:2},2).addProduct({id:8,name:'Jugo',unitPrice:3},1);
    const cases={pendingSale:()=>null,activeSession:vi.fn().mockResolvedValue({id:3}),quoteSale:vi.fn().mockResolvedValue({sessionId:3,total:7,lines:[],methods:[{id:1,name:'Efectivo'}]})};
    const flow=createSalesPresenter(cases);const sync=()=>flow.setSale({lines:cart.toRequestLines(),discountCode:'none'});sync();let allowed=true;
    const remove=vi.fn(id=>{cart=cart.removeProduct(id);sync();});const removal=createRemovalPresenter({readCart:()=>cart,remove,canRemove:()=>allowed});
    return {readCart:()=>cart,flow,removal,remove,setAllowed:value=>{allowed=value;}};
  }
  it('opening/cancel and Escape equivalent preserve cart identity and verified quote',async()=>{
    const x=setupRemoval();await x.flow.quote();const cart=x.readCart(),quote=x.flow.read().quote;
    x.removal.request(7);expect(x.removal.read().target.name).toBe('Café');expect(x.remove).not.toHaveBeenCalled();x.removal.cancel();expect(x.readCart()).toBe(cart);expect(x.flow.read().quote).toBe(quote);
    x.removal.request(8);cancelDialog({preventDefault:noop},x.removal.cancel);expect(x.readCart()).toBe(cart);expect(x.flow.read().quote).toBe(quote);
  });
  it('confirmed removal changes only selected line, invalidates quote and prevents a double action',async()=>{
    const x=setupRemoval();await x.flow.quote();x.flow.acceptReview(true);x.removal.request(7);expect(x.removal.confirm()).toBe(true);expect(x.removal.confirm()).toBe(false);
    expect(x.remove).toHaveBeenCalledExactlyOnceWith(7);expect(x.readCart().lines.map(line=>line.productId)).toEqual([8]);expect(x.flow.read().quote).toBeNull();expect(x.flow.read().reviewAccepted).toBe(false);
  });
  it('blocks a reentrant confirmation synchronously before notifying listeners',()=>{
    const x=setupRemoval();x.removal.request(7);const reentrant=vi.fn();
    const unsubscribe=x.removal.subscribe(()=>{if(x.removal.read().busy)reentrant(x.removal.confirm());});
    expect(x.removal.confirm()).toBe(true);unsubscribe();
    expect(reentrant).toHaveBeenCalledExactlyOnceWith(false);expect(x.remove).toHaveBeenCalledExactlyOnceWith(7);
  });
  it('blocks removal if a critical operation starts after the dialog opens',()=>{
    const x=setupRemoval();x.removal.request(7);x.setAllowed(false);expect(x.removal.confirm()).toBe(false);expect(x.remove).not.toHaveBeenCalled();expect(x.removal.read().error).not.toBe('');x.removal.cancel();expect(x.removal.read().target).toBeNull();
  });
  it('keeps the selected target stable and ignores a missing product',()=>{
    const x=setupRemoval();expect(x.removal.request(99)).toBe(false);x.removal.request(7);expect(x.removal.request(8)).toBe(false);expect(x.removal.read().target.productId).toBe(7);
  });
});

describe('section navigation remains visible, active and exclusive',()=>{
  it.each(['venta','cobro','historial','operaciones'])('identifies active section for %s',initialView=>{
    const html=renderToStaticMarkup(<PosPage initialView={initialView} useCases={{pendingSale:()=>null}} username="a" onLogout={noop} />);
    const section=initialView==='cobro'?'venta':initialView;
    expect(html).toContain('primary-sections');expect(html).toContain(`href="#${section}" aria-current="page"`);
    if(initialView==='venta')expect(html).toContain('sale-columns');else expect(html).not.toContain('sale-columns');
    if(['venta','cobro'].includes(initialView))expect(html).toContain('Pasos de venta');else expect(html).not.toContain('Pasos de venta');
  });
});
