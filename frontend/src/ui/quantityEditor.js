// UI grammar supplements (never replaces) domain and server validation.
const invalid = 'Use solo dígitos del 0 al 9 para una cantidad entera positiva.';
const isDraft = value => typeof value === 'string' && /^[0-9]*$/.test(value) &&
  (value === '' || Number.isSafeInteger(Number(value)));
export function createQuantityEditor(quantity, change) {
  let state = { draft: String(quantity), error: '' };
  return {
    read: () => state,
    step(delta) {
      // Explicit +/- uses the confirmed domain value, never an ambiguous draft.
      return this.commit(String(quantity + delta));
    },
    edit(value) {
      if (!isDraft(value)) {
        state = { draft: state.draft, error: invalid };
      } else {
        state = { draft: value, error: '' };
      }
      return state;
    },
    commit(actualValue = state.draft) {
      // Re-read the control: programmatic changes can bypass onChange.
      try {
        if (!isDraft(actualValue) || actualValue === '') throw new Error(invalid);
        const value = Number(actualValue);
        if (value !== quantity) change(value);
        quantity = value;
        state = { draft: String(value), error: '' };
      } catch (error) {
        state = { draft: String(quantity), error: error.message, restored: true };
      }
      return state;
    },
  };
}

// Both keyboard Enter and blur use this exact validation path.
export function commitQuantityControl(editor, event) {
  if (event.isComposing || event.nativeEvent?.isComposing) return null;
  if (event.type === 'keydown' && event.key !== 'Enter') return null;
  if (event.type === 'keydown') event.preventDefault();
  return editor.commit(event.currentTarget.value);
}
