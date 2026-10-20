import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';
test('panel: confirmed perk unlocks the next purchase while cloud GET is still pending',async({page})=>{
 const old=profile(20000),committed=profile(18000);committed.rpg.perks.damage=3;committed.updatedAt='2026-10-18T12:00:01Z';
 const rules:any=catalog();rules.perks[0].Costs=['500','1000','2000','4000','8000'];
 let applied=false,posts=0;let delayed:any;
 await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
 await page.route(/^https:\/\/(?:rc1---)?funjii-otter-rpg-backend-test-cwzl75ajla-ey\.a\.run\.app\//,async r=>{
  const url=r.request().url();
  if(url.endsWith('/me')){if(applied){delayed=r;return;}return r.fulfill({json:old});}
  if(url.endsWith('/state'))return r.fulfill({json:{available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(rules)}});
  const intent=r.request().postDataJSON();expect(intent.kind).toBe('buyPerk');posts++;applied=true;
  return r.fulfill({status:202,json:{actionId:intent.actionId,state:'applied',outcome:{success:true,dataJson:JSON.stringify({Balance:18000,profile:committed,stats:{Damage:6}})}}});
 });
 await page.setViewportSize({width:320,height:500});await page.goto('/panel.html');
 await page.getByRole('button',{name:'Perks',exact:true}).click();
 await page.getByRole('button',{name:'Kaufen · 2.000 Perlen',exact:true}).click();
 const next=page.getByRole('button',{name:'Kaufen · 4.000 Perlen',exact:true});
 await expect(next).toBeEnabled();expect(posts).toBe(1);
 await expect(page.locator('.statusbar')).toContainText('18.000');
 await next.scrollIntoViewIfNeeded();await page.screenshot({path:'test-results/perk-confirmation-panel.png'});
 expect(delayed).toBeTruthy();await delayed.fulfill({json:old});
 await expect(next).toBeEnabled();await expect(page.locator('.statusbar')).toContainText('18.000');
});
