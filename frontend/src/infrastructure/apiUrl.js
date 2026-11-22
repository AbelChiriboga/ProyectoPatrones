// Resolución de la URL base del backend .NET:
// 1. Prioridad: variable de entorno VITE_API_URL (ej. en .env, .env.lan o inyectada por el entorno).
// 2. Dinámica: si se ejecuta en navegador, infiere automáticamente el host de window.location.
//    (ej. si la PC cliente abre http://192.168.1.50:5173, el API resuelve a http://192.168.1.50:5038).
// 3. Fallback: http://localhost:5038 para desarrollo local estándar o entornos sin ventana global.
export function resolveApiBaseUrl(env = import.meta.env, win = typeof window !== 'undefined' ? window : null) {
  if (env?.VITE_API_URL) {
    return env.VITE_API_URL;
  }

  if (win?.location?.hostname) {
    const protocol = win.location.protocol || 'http:';
    const hostname = win.location.hostname;
    return `${protocol}//${hostname}:5038`;
  }

  return 'http://localhost:5038';
}
