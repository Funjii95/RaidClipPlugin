import {test,expect} from '@playwright/test';
import fs from 'node:fs';
import {profile,availability,authorization} from './remote-fixtures';
async function prepare(page:any){
 await page.route('https://extension-files.twitch.tv/**',(r:any)=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
 await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',(r:any)=>r.fulfill({json:r.request().url().endsWith('/me')?profile():{...availability(),streamLive:false}}));
}
for(const [view,width,height]of [['panel',320,500],['mobile',390,844],['mobile',844,390]]as const){
 test(`release ${view} ${width}x${height}: viewport and CSP`,async({page})=>{
  await prepare(page);await page.setViewportSize({width,height});const errors:string[]=[];page.on('console',m=>{if(/content security policy|refused to/i.test(m.text()))errors.push(m.text());});page.on('pageerror',e=>errors.push(e.message));
  const reply=await page.goto('/'+view+'.html');expect(reply?.headers()['content-security-policy']).toContain('connect-src https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app');
  await expect(page.locator('.statusbar')).toContainText('1.234');expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);expect(await page.title()).not.toContain('Demo');
  await page.getByRole('button',{name:'Inventar',exact:true}).click();await expect(page.getByRole('heading',{name:'Gespeicherter Dreizack'})).toBeVisible();expect(errors).toEqual([]);
  await page.screenshot({path:`test-results/release-${view}-${width}x${height}.png`});
 });
}
test.skip('DEFERRED by owner: mobile initial CDN payload is at most 1MB',async({page})=>{
 await prepare(page);await page.setViewportSize({width:390,height:844});const pending:Promise<void>[]=[];const resources:{path:string,bytes:number}[]=[];
 page.on('response',r=>{if(r.url().startsWith('https://localhost:8080/'))pending.push(r.body().then(b=>{resources.push({path:new URL(r.url()).pathname,bytes:b.length});}));});
 await page.goto('/mobile.html');await expect(page.locator('.statusbar')).toContainText('1.234');await page.waitForLoadState('networkidle');await Promise.all(pending);
 const bytes=resources.reduce((n,r)=>n+r.bytes,0);fs.writeFileSync('test-results/release-mobile-payload.json',JSON.stringify({bytes,limit:1000000,resources,passed:bytes<=1000000},null,2));
 expect(bytes,'Twitch mobile initial CDN payload exceeds 1MB; release remains blocked.').toBeLessThanOrEqual(1000000);
});
test('release submission screenshot captures the real adapter UI in 4:3',async({page})=>{
 await prepare(page);await page.setViewportSize({width:1024,height:768});await page.goto('/panel.html');await expect(page.locator('.statusbar')).toContainText('1.234');await page.screenshot({path:'test-results/review-screenshot-1024x768.png'});
});
