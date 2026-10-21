import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';
for(const tab of ['Inventar','Ausrüstung'])test('panel: upgrade preview in '+tab+' never mutates inventory or currency',async({page})=>{
 const saved=profile(1000000,true);let posts=0;
 await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:'window.Twitch={ext:{onAuthorized:cb=>cb('+JSON.stringify(authorization)+'),actions:{requestIdShare:()=>{}}}};'}));
 await page.route(url=>url.hostname.endsWith('funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app'),r=>{
  if(r.request().method()==='POST'){posts++;return r.fulfill({status:500});}
  return r.fulfill({json:r.request().url().endsWith('/me')?saved:{available:true,reason:'ready',sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(catalog())}});
 });
 await page.setViewportSize({width:320,height:500});await page.goto('/panel.html');
 await page.getByRole('button',{name:tab,exact:true}).click();
 const card=page.locator('[data-instance-id="owned-trident"]');await expect(card).toBeVisible();
 await card.getByRole('button',{name:'Aufwerten',exact:true}).click();
 const dialog=page.getByRole('dialog',{name:'Ausrüstung aufwerten'});
 await expect(dialog).toBeVisible();await expect(dialog.getByRole('heading')).toBeInViewport();await expect(dialog).toContainText('Gespeicherter Dreizack');
 await expect(dialog).toContainText('+9: 1.000.000 Perlen + 1 rote Perle');
 await expect(dialog).toContainText('Aufwertung noch nicht freigeschaltet');
 await expect(dialog).toContainText('Es werden keine Perlen oder Materialien verbraucht');
 await page.screenshot({path:'test-results/upgrade-preview-'+(tab==='Inventar'?'inventory':'equipment')+'.png',fullPage:false});
 await dialog.getByRole('button',{name:'Schließen',exact:true}).click();
 await expect(dialog).not.toBeVisible();await expect(card).toBeVisible();expect(posts).toBe(0);
});
