// `vitest/config` rather than `vite`, so the `test` block below is typed.
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// In development Vite serves the SPA and proxies the API to the ASP.NET Core server.
// In production the server serves the built output from wwwroot and there is no proxy.
const apiTarget = process.env.API_TARGET ?? 'http://localhost:5182';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: false },
    },
  },
  build: {
    // Published straight into the server's wwwroot so `dotnet run` serves the SPA.
    outDir: '../Acme.Server/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
  },
});
