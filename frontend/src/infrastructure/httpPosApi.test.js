import { describe, expect, it, vi } from 'vitest';
import { ApiError, createHttpPosApi } from './httpPosApi.js';

const jsonResponse = (status, body) => ({ ok: status >= 200 && status < 300, status, json: () => Promise.resolve(body) });

function setup(fetchFn, token = 'jwt') {
  const onUnauthorized = vi.fn();
  const api = createHttpPosApi({ baseUrl: 'http://api.test', getToken: () => token, onUnauthorized, fetchFn });
  return { api, onUnauthorized };
}

describe('httpPosApi', () => {
  it('sends the bearer token and encodes the search term', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse(200, []));
    const { api } = setup(fetchFn);

    await api.searchProducts('café & té');

    const [url, options] = fetchFn.mock.calls[0];
    expect(url).toBe('http://api.test/api/products?search=caf%C3%A9%20%26%20t%C3%A9');
    expect(options.headers.Authorization).toBe('Bearer jwt');
  });

  it('login does not send the token', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse(200, { token: 'x' }));
    const { api } = setup(fetchFn);

    await api.login({ username: 'a', password: 'b' });

    expect(fetchFn.mock.calls[0][1].headers.Authorization).toBeUndefined();
    expect(fetchFn.mock.calls[0][1].body).toBe('{"username":"a","password":"b"}');
  });

  it('turns ProblemDetails into an ApiError with the API code', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse(422, { code: 'discount.not_authorized', detail: 'No autorizado' }));
    const { api } = setup(fetchFn);

    await expect(api.previewSale({ lines: [] })).rejects.toMatchObject({ status: 422, code: 'discount.not_authorized', message: 'No autorizado' });
  });

  it('notifies when the session is no longer valid', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse(401, {}));
    const { api, onUnauthorized } = setup(fetchFn);

    await expect(api.listDiscounts()).rejects.toBeInstanceOf(ApiError);
    expect(onUnauthorized).toHaveBeenCalled();
  });

  it('a wrong login does not trigger the session-expired flow', async () => {
    const fetchFn = vi.fn().mockResolvedValue(jsonResponse(401, { code: 'auth.invalid_credentials', detail: 'Usuario o contraseña incorrectos.' }));
    const { api, onUnauthorized } = setup(fetchFn);

    await expect(api.login({ username: 'a', password: 'b' })).rejects.toMatchObject({ code: 'auth.invalid_credentials' });
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('reports a network failure with a clear message', async () => {
    const fetchFn = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    const { api } = setup(fetchFn);

    await expect(api.listDiscounts()).rejects.toMatchObject({ code: 'network.unavailable' });
  });
});
