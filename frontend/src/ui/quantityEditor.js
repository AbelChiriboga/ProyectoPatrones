// Presentation draft: domain/application remain responsible for quantity and stock rules.
export function createQuantityEditor(quantity, change) {
  let state = { draft: String(quantity), error: '' };
  return {
    read: () => state,
    edit(value) { state = { draft: value, error: '' }; return state; },
    commit() {
      try {
        const value = state.draft.trim() === '' ? NaN : Number(state.draft);
        change(value);
        quantity = value;
        state = { draft: String(value), error: '' };
      } catch (error) {
        state = { draft: String(quantity), error: error.message };
      }
      return state;
    },
  };
}
