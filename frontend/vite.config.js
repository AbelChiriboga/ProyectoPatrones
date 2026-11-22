import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// El puerto 5173 es el origen que la API permite en CORS (Cors:AllowedOrigins).
// host: '0.0.0.0' expone Vite en todas las interfaces de red para acceso simultáneo desde localhost y LAN.
export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
  },
  preview: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
  },
});
