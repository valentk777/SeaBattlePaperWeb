import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: '/sea-battle-paper/',
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/sea-battle-paper/ship-api": {
        target: "http://localhost:5077",
        changeOrigin: true
      },
      "/sea-battle-paper/ship-hubs": {
        target: "http://localhost:5077",
        changeOrigin: true,
        ws: true
      }
    }
  },
  build: {
    outDir: "../SeaBattlePaper.Api/wwwroot",
    emptyOutDir: true
  }
});
