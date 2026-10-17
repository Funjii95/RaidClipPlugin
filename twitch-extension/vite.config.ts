import { defineConfig } from "vite";
export default defineConfig({
  base: "./",
  build: {
    rollupOptions: { input: ["panel.html", "mobile.html", "config.html"] },
  },
});
