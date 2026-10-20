import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';
for(const [view,width,height] of [['panel',320,500],['mobile',390,844]] as const){
 test(`${view}: character shows plugin stats while pearls differ from cloud`,async({page})=>{
  const current=profile();const origin=profile(9999);origin.rpg.health.currentHp=12;
  await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
  await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',async r=>{
   const url=r.request().url();
   if(url.endsWith('/me'))return r.fulfill({json:current});
   if(url.endsWith('/state'))return r.fulfill({json:{available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify({...catalog(),supported:['status']})}});
   const intent=r.request().postDataJSON();expect(intent.kind).toBe('status');
   return r.fulfill({status:202,json:{actionId:intent.actionId,state:'applied',outcome:{success:true,dataJson:JSON.stringify({profile:origin,stats:{Damage:12.5,CritChance:3,Power:7}})}}});
  });
  await page.setViewportSize({width,height});await page.goto('/'+view+'.html');
  const damage=page.locator('.stat').filter({has:page.getByText('Schadensbonus',{exact:true})});
  await expect(damage).toContainText('12.5 %');await expect(page.locator('.pearl-count, .statusbar')).toContainText('1.234');
  await damage.scrollIntoViewIfNeeded();await page.screenshot({path:`test-results/character-stats-${view}.png`});
  await page.reload();await expect(damage).toContainText('12.5 %');
 });
}
