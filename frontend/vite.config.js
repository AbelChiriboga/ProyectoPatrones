import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// El puerto 5173 es el origen que la API permite en CORS (Cors:AllowedOrigins).
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
});
