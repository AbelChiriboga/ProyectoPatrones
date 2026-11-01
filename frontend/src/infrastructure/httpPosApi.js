// Adaptador HTTP del puerto "api": habla con la API .NET (nunca directamente con Odoo).

export class ApiError extends Error {
  constructor(status, code, message) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

export function createHttpPosApi({ baseUrl, getToken, onUnauthorized, fetchFn = (...args) => fetch(...args) }) {
  async function request(path, { method = 'GET', body, authenticated = true } = {}) {
    const headers = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    const token = getToken();
    if (authenticated && token) headers.Authorization = `Bearer ${token}`;

    let response;
    try {
      response = await fetchFn(`${baseUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch {
      throw new ApiError(0, 'network.unavailable', 'No se pudo conectar con el servidor. Verifique que la API esté en ejecución.');
    }

    if (response.status === 401 && authenticated) {
      onUnauthorized?.();
      throw new ApiError(401, 'auth.session_expired', 'Su sesión expiró. Inicie sesión nuevamente.');
    }

    if (!response.ok) {
      // La API responde siempre con ProblemDetails: { detail, code }.
      const problem = await response.json().catch(() => ({}));
      throw new ApiError(response.status, problem.code ?? 'server.error', problem.detail ?? 'Ocurrió un error inesperado.');
    }
    return response.json();
  }

  return {
    login: (credentials) => request('/api/auth/login', { method: 'POST', body: credentials, authenticated: false }),
    searchProducts: (term) => request(`/api/products?search=${encodeURIComponent(term)}`),
    listDiscounts: () => request('/api/discounts'),
    previewSale: (sale) => request('/api/sales/preview', { method: 'POST', body: sale }),
  };
}
