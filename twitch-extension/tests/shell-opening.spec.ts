import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';

for(const online of [true,false]){
 test('panel: shells '+(online?'use the authenticated action and committed stock':'remain read-only when the plugin is offline'),async({page})=>{
  const saved:any=profile(1000);saved.rpg.materials={shells:10,whitePearls:0,bluePearls:0,redPearls:0,stoneChips:0};
  const committed=structuredClone(saved);committed.updatedAt='2026-10-18T12:00:01Z';committed.rpg.materials={shells:0,whitePearls:3,bluePearls:2,redPearls:1,stoneChips:4};
  const rules=catalog();rules.supported.push('openShells');let posts=0;
  await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:'window.Twitch={ext:{onAuthorized:cb=>cb('+JSON.stringify(authorization)+'),actions:{requestIdShare:()=>{}}}};'}));
  await page.route(url=>url.hostname.endsWith('funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app'),async r=>{
   const url=r.request().url();
   if(url.endsWith('/me'))return r.fulfill({json:saved});
   if(url.endsWith('/state'))return r.fulfill({json:{available:online,reason:online?'ready':'plugin_not_ready',sessionId:online?'11111111-1111-4111-8111-111111111111':null,catalogJson:JSON.stringify(rules)}});
   const intent=r.request().postDataJSON();posts++;
   expect(intent.kind).toBe('openShells');expect(intent.arguments).toEqual({count:'10'});
   return r.fulfill({status:202,json:{actionId:intent.actionId,state:'applied',outcome:{success:true,message:'10 Muscheln geöffnet.',dataJson:JSON.stringify({profile:committed})}}});
  });
  await page.setViewportSize({width:320,height:500});await page.goto('/panel.html');
  await page.getByRole('button',{name:'Perlen-Minispiele',exact:true}).click();
  const box=page.locator('article').filter({has:page.getByRole('heading',{name:'Muscheln öffnen',exact:true})});
  await expect(box).toContainText('Muscheln: 10');
  await expect(box.getByRole('button',{name:'100 öffnen',exact:true})).toBeDisabled();
  if(online){
   await box.getByRole('button',{name:'10 öffnen',exact:true}).click();
   await expect(box).toContainText('Muscheln: 0');await expect(box).toContainText('3 weiß');
   await expect(box.getByRole('button',{name:'1 öffnen',exact:true})).toBeDisabled();
   expect(posts).toBe(1);
   await box.scrollIntoViewIfNeeded();await page.screenshot({path:'test-results/shell-opening-panel.png',fullPage:true});
  }else{
   await expect(box.getByRole('button',{name:'1 öffnen',exact:true})).toBeDisabled();
   expect(posts).toBe(0);
  }
 });
}
