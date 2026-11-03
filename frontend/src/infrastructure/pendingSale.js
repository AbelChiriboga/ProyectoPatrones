// A pending UUID survives reload and logout; each operator has a separate intention.
export function createPendingSale(storage = localStorage, newId = () => crypto.randomUUID()) {
  const key = (owner) => `pos.pending.${owner}`;
  return {
    newId,
    read: (owner) => JSON.parse(storage.getItem(key(owner)) ?? 'null'),
    save: (owner, request) => storage.setItem(key(owner), JSON.stringify(request)),
    clear: (owner) => storage.removeItem(key(owner)),
  };
}
