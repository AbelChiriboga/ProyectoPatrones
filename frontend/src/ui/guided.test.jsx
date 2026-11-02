import { describe, expect, it, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import PosPage from './PosPage.jsx';
import { createUseCases } from '../application/useCases.js';
import { createQuantityEditor } from './quantityEditor.js';
import { createSalesPresenter } from './salesPresenter.js';

describe('SDD-03.4-C defects reproduced before correction',()=>{
  it.each(['x'.repeat(101),'café\u0000','!!!'])('DEF-04 rejects invalid query before HTTP: %j',term=>{
    const api={searchProducts:vi.fn()},cases=createUseCases({api,session:{}});
    expect(()=>cases.searchProducts(term)).toThrow();expect(api.searchProducts).not.toHaveBeenCalled();
  });
  it('DEF-05 clears a rejected attempt when the actual control contains the valid confirmed value',()=>{
    const change=vi.fn(),editor=createQuantityEditor(1,change);
    editor.edit('x');expect(editor.read().error).not.toBe('');
    expect(editor.commit('1').error).toBe('');expect(change).not.toHaveBeenCalled();
  });
  it('DEF-06 starts loading history on entry without a preliminary button',async()=>{
    const cases={pendingSale:()=>null,salesHistoryPage:vi.fn().mockResolvedValue({items:[],hasMore:false,nextBeforeId:null})};
    const presenter=createSalesPresenter(cases);await presenter.enterView('historial');
    expect(cases.salesHistoryPage).toHaveBeenCalledTimes(1);expect(presenter.read().historyLoaded).toBe(true);
  });
  it('DEF-07 separates the guided checkout step from the two-column sale',()=>{
    const props={useCases:{pendingSale:()=>null},username:'a',onLogout:()=>{}};
    const sale=renderToStaticMarkup(<PosPage {...props} />);
    expect(sale).toContain('href="#cobro"');expect(sale).toContain('sale-columns');
    const checkout=renderToStaticMarkup(<PosPage {...props} initialView="cobro" />);
    expect(checkout).not.toContain('Buscar productos');expect(checkout).toContain('Revisión y pago');
  });
});
