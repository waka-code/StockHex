import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    // 5173 ya está en la lista de orígenes permitidos de la API en desarrollo.
    port: 5173,
    strictPort: true,
  },
  build: {
    sourcemap: true,
  },
});
