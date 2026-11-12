import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { createSalesPresenter } from './salesPresenter.js';
import { HistoryView } from './SalesViews.jsx';
const row=id=>({id,reference:`Caja ${id}`,dateUtc:null,total:1,currency:'USD',state:'paid'});
const page=(id,more=false)=>({items:id?[row(id)]:[],hasMore:more,nextBeforeId:more?id:null});
const deferred=()=>{let resolve;const promise=new Promise(r=>{resolve=r;});return {promise,resolve};};
const setup=()=>{const api={pendingSale:()=>null,salesHistoryPage:vi.fn().mockResolvedValue(page(50)),saleDetail:vi.fn().mockResolvedValue({...row(50),lines:[],methods:[],paid:1,tax:0})};return {api,p:createSalesPresenter(api)};};
beforeEach(()=>vi.useFakeTimers());afterEach(()=>{vi.clearAllTimers();vi.useRealTimers();});
it('automatically searches once after 300 ms, coalescing typing without Enter',async()=>{
 const {api,p}=setup();await p.enterView('historial');p.editHistorySearch('C');await vi.advanceTimersByTimeAsync(200);p.editHistorySearch('Ca');p.editHistorySearch('Caja');await vi.advanceTimersByTimeAsync(299);expect(api.salesHistoryPage).toHaveBeenCalledTimes(1);await vi.advanceTimersByTimeAsync(1);expect(api.salesHistoryPage).toHaveBeenCalledTimes(2);expect(api.salesHistoryPage).toHaveBeenLastCalledWith({search:'Caja',beforeId:0,limit:20});p.editHistorySearch(' Caja ');await vi.advanceTimersByTimeAsync(500);expect(api.salesHistoryPage).toHaveBeenCalledTimes(2);
});
it('resets paging and automatically restores general history when cleared',async()=>{
 const {api,p}=setup();api.salesHistoryPage.mockResolvedValueOnce(page(50,true)).mockResolvedValue(page(49));await p.enterView('historial');await p.nextHistory();expect(p.read().historyIndex).toBe(1);p.editHistorySearch('002');expect(p.read().historyIndex).toBe(0);await vi.advanceTimersByTimeAsync(300);expect(api.salesHistoryPage).toHaveBeenLastCalledWith({search:'002',beforeId:0,limit:20});p.editHistorySearch('');await vi.advanceTimersByTimeAsync(300);expect(api.salesHistoryPage).toHaveBeenLastCalledWith({search:'',beforeId:0,limit:20});
});
it('invalidates old HTTP responses immediately, including during the debounce interval',async()=>{
 const {api,p}=setup();const old=deferred();api.salesHistoryPage.mockReturnValueOnce(old.promise);const initial=p.enterView('historial');p.editHistorySearch('new');old.resolve(page(99));await initial;expect(p.read().history).toEqual([]);await vi.advanceTimersByTimeAsync(300);expect(p.read().history[0].id).toBe(50);
 const late=deferred();api.salesHistoryPage.mockReturnValueOnce(late.promise);p.editHistorySearch('older');await vi.advanceTimersByTimeAsync(300);p.editHistorySearch('latest');await vi.advanceTimersByTimeAsync(300);late.resolve(page(9));await Promise.resolve();expect(p.read().historyQuery).toBe('latest');expect(p.read().history[0].id).toBe(50);
});
it('keeps the table visible during loading and shows empty results after completion',async()=>{
 const {api,p}=setup();await p.enterView('historial');api.salesHistoryPage.mockResolvedValue(page(null));p.editHistorySearch('missing');const loading=renderToStaticMarkup(<HistoryView state={p.read()} />);expect(loading).toContain('<table');expect(loading).toContain('Cargando órdenes');expect(loading).toContain('aria-busy="true"');expect(loading).not.toContain('type="submit"');await vi.advanceTimersByTimeAsync(300);const empty=renderToStaticMarkup(<HistoryView state={p.read()} />);expect(empty).toContain('No se encontraron órdenes');expect(empty).not.toContain('<table');
});
it('recovers from connection failure automatically on the next valid edit',async()=>{
 const {api,p}=setup();await p.enterView('historial');api.salesHistoryPage.mockRejectedValueOnce(new Error('network'));p.editHistorySearch('failed');await vi.advanceTimersByTimeAsync(300);expect(p.read().historyStatus).toBe('error');expect(renderToStaticMarkup(<HistoryView state={p.read()} />)).toContain('Reintentar historial');p.editHistorySearch('recovered');await vi.advanceTimersByTimeAsync(300);expect(p.read().historyStatus).toBe('ready');expect(p.read().historyError).toBe('');
});
it('preserves printable Odoo references and rejects invalid input without a request',async()=>{
 const {api,p}=setup();await p.enterView('historial');p.editHistorySearch('Caja / 000-002 (á)');await vi.advanceTimersByTimeAsync(300);expect(p.read().historyQuery).toBe('Caja / 000-002 (á)');for(const invalid of ['x'.repeat(101),'bad\n','bad\u200b'])p.editHistorySearch(invalid);await vi.advanceTimersByTimeAsync(300);expect(api.salesHistoryPage).toHaveBeenCalledTimes(2);expect(p.read().historySearchError).not.toBe('');
});
it('cancels queued searches on navigation/disposal and retains modal detail behavior',async()=>{
 const {api,p}=setup();await p.enterView('historial');await p.detail(50);expect(p.read().detailOpen).toBe(true);p.closeDetail();p.editHistorySearch('queued');await p.enterView('venta');await vi.advanceTimersByTimeAsync(500);expect(api.salesHistoryPage).toHaveBeenCalledTimes(1);await p.enterView('historial');expect(api.salesHistoryPage).toHaveBeenLastCalledWith({search:'queued',beforeId:0,limit:20});p.editHistorySearch('disposed');p.dispose();await vi.advanceTimersByTimeAsync(500);expect(api.salesHistoryPage).toHaveBeenCalledTimes(2);
});
