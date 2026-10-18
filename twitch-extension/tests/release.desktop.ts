import {test,expect} from '@playwright/test';
import {profile,availability,authorization} from './remote-fixtures';
async function prepare(page:any){await page.route('https://extension-files.twitch.tv/**',(r:any)=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',(r:any)=>r.fulfill({json:r.request().url().endsWith('/me')?profile():{...availability(),streamLive:false}}));}
test('desktop release: real adapter, 320x500, no CSP errors, offline stream can play',async({page,request})=>{
 await prepare(page);await page.setViewportSize({width:320,height:500});const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(/content security policy|refused to/i.test(m.text()))errors.push(m.text());});
 await page.goto('/panel.html');await expect(page.locator('.statusbar')).toContainText('1.234');await expect(page.locator('.statusbar')).toContainText('Plugin online');expect(await page.title()).not.toContain('Demo');expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.getByRole('button',{name:'Inventar',exact:true}).click();await expect(page.getByRole('button',{name:'Ausrüsten',exact:true})).toBeEnabled();expect(errors).toEqual([]);await page.screenshot({path:'test-results/release-desktop-panel-320x500.png'});
 expect((await request.get('/mobile.html')).status()).toBe(404);
 await page.goto('/config.html');await expect(page.getByRole('heading',{name:'Extension-Konfiguration'})).toBeVisible();
});
test('desktop submission screenshot, 1024x768',async({page})=>{await prepare(page);await page.setViewportSize({width:1024,height:768});await page.goto('/panel.html');await expect(page.locator('.statusbar')).toContainText('1.234');await page.screenshot({path:'test-results/review-desktop-1024x768.png'});});
