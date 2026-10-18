import {defineConfig} from '@playwright/test';
export default defineConfig({testDir:'./tests',testMatch:'**/release.desktop.ts',workers:1,use:{baseURL:'https://localhost:8080',ignoreHTTPSErrors:true},webServer:{command:'node scripts/serve.mjs --desktop-release',url:'https://localhost:8080/panel.html',ignoreHTTPSErrors:true,reuseExistingServer:false}});
