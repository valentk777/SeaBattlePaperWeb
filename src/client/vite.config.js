import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
//   base: '/sea-battle-paper/',
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:5077",
        changeOrigin: true
      },
      "/hubs": {
        target: "http://localhost:5077",
        ws: true,
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: "../SeaBattlePaper.Api/wwwroot",
    emptyOutDir: true
  }
});
