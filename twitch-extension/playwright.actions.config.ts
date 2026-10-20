import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: "**/*.spec.ts",
  use: { baseURL: "https://localhost:8083", ignoreHTTPSErrors: true },
  workers: 1,
  webServer: {
    command: "node scripts/serve-actions-test.mjs",
    url: "https://localhost:8083/panel.html",
    ignoreHTTPSErrors: true,
    reuseExistingServer: false,
  },
});
