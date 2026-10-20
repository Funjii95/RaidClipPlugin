import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';

for(const [view,width,height] of [['panel',320,500],['mobile',390,844]] as const){
 test(`${view}: boss countdown uses server deadline and survives reload`,async({page})=>{
  const start=Date.parse('2026-10-20T12:00:00Z');
  await page.clock.install({time:start});
  const boss={status:{BossActive:true,BossHp:180,BossMaxHp:200,BossEventId:'timer-boss',BossEndsAt:new Date(start+65000).toISOString() as string|null},damage:20};
  await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
  await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',async r=>{
   const url=r.request().url();
   if(url.endsWith('/me'))return r.fulfill({json:profile()});
   if(url.endsWith('/state'))return r.fulfill({json:{available:true,sessionId:'11111111-1111-4111-8111-111111111111',streamLive:false,catalogJson:JSON.stringify({...catalog(),supported:['bossStatus','bossAttack']})}});
   const intent=r.request().postDataJSON();expect(intent.kind).toBe('bossStatus');
   return r.fulfill({status:202,json:{actionId:intent.actionId,state:'applied',outcome:{success:true,code:'applied',message:'',dataJson:JSON.stringify(boss)}}});
  });
  await page.setViewportSize({width,height});await page.goto('/'+view+'.html');
  await expect(page.locator('.statusbar')).toContainText('Plugin online');
  await page.getByRole('button',{name:'Boss',exact:true}).click();
  const timer=page.getByLabel('Boss-Restzeit');
  await expect(timer).toHaveText('Verbleibende Zeit: 1:05');
  await page.clock.runFor(1000);await expect(timer).toHaveText('Verbleibende Zeit: 1:04');
  await page.reload();await page.getByRole('button',{name:'Boss',exact:true}).click();
  await expect(timer).toHaveText('Verbleibende Zeit: 1:04');
  await timer.scrollIntoViewIfNeeded();await page.screenshot({path:`test-results/boss-timer-${view}.png`});
  await page.clock.fastForward(64000);await expect(timer).toContainText('Zeit abgelaufen');
  boss.status.BossEndsAt=null;
  await page.reload();await page.getByRole('button',{name:'Boss',exact:true}).click();
  await expect(timer).toHaveText('Verbleibende Zeit: nicht übermittelt');
  boss.status.BossActive=false;
  await page.reload();await page.getByRole('button',{name:'Boss',exact:true}).click();
  await expect(timer).toHaveCount(0);
 });
}
