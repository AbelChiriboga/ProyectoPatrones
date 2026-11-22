import { describe, expect, it } from 'vitest';
import { resolveApiBaseUrl } from './apiUrl.js';

describe('resolveApiBaseUrl', () => {
  it('prioritizes VITE_API_URL when provided in environment', () => {
    const env = { VITE_API_URL: 'http://custom-server:5038' };
    const win = { location: { protocol: 'http:', hostname: '192.168.1.100' } };
    expect(resolveApiBaseUrl(env, win)).toBe('http://custom-server:5038');
  });

  it('resolves dynamically to client hostname on LAN when VITE_API_URL is unset', () => {
    const env = {};
    const win = { location: { protocol: 'http:', hostname: '192.168.1.50' } };
    expect(resolveApiBaseUrl(env, win)).toBe('http://192.168.1.50:5038');
  });

  it('resolves dynamically to localhost when accessed locally without env var', () => {
    const env = {};
    const win = { location: { protocol: 'http:', hostname: 'localhost' } };
    expect(resolveApiBaseUrl(env, win)).toBe('http://localhost:5038');
  });

  it('respects https protocol if served over secure connection', () => {
    const env = {};
    const win = { location: { protocol: 'https:', hostname: 'pos.lan' } };
    expect(resolveApiBaseUrl(env, win)).toBe('https://pos.lan:5038');
  });

  it('falls back to http://localhost:5038 if window is not available', () => {
    const env = {};
    expect(resolveApiBaseUrl(env, null)).toBe('http://localhost:5038');
  });
});
