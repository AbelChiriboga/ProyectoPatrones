// Adaptador del puerto "session": guarda el token en sessionStorage (se borra al cerrar la pestaña).
// El acceso a storage puede fallar (modo privado, bloqueado), por eso todo va en try/catch.

const KEY = 'pos.session';

export function createBrowserSession() {
  return {
    read() {
      try {
        const raw = sessionStorage.getItem(KEY);
        return raw ? JSON.parse(raw) : null;
      } catch {
        return null;
      }
    },
    save(token, username) {
      try {
        sessionStorage.setItem(KEY, JSON.stringify({ token, username }));
      } catch {
        // Sin storage la sesión solo dura mientras no se recargue la página.
      }
    },
    clear() {
      try {
        sessionStorage.removeItem(KEY);
      } catch {
        // nada que limpiar
      }
    },
  };
}
