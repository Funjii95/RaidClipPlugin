import {test,expect} from '@playwright/test';
test('live panel uses verified backend actions and snapshots with no mock mutation',async({page})=>{
 let applied=false;let actionId='';
 await page.route('https://extension-files.twitch.tv/**',route=>route.fulfill({contentType:'text/javascript',body:'window.Twitch={ext:{onAuthorized:cb=>cb({token:"synthetic",channelId:"42",userId:"Usynthetic"})}}'}));
 await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',async route=>{
  const url=route.request().url();
  if(url.endsWith('/state'))return route.fulfill({json:{available:true,sessionId:'session-1',reason:'ready',catalogJson:JSON.stringify({perks:[{Id:'damage',Name:'Stärke',MaximumLevel:10}]})}});
  if(url.endsWith('/me'))return route.fulfill({json:{displayName:'SYNTHETIC',points:applied?1500:2000,rpg:{perks:{damage:applied?1:0},inventory:[],equipment:{}}}});
  if(route.request().method()==='POST') {const body=route.request().postDataJSON();expect(body.userId).toBeUndefined();expect(body.kind).toBe('buyPerk');actionId=body.actionId;applied=true;return route.fulfill({status:202,json:{actionId,state:'queued'}});}
  return route.fulfill({json:{actionId,state:'applied',outcome:{success:true,code:'applied',message:'Gekauft'}}});
 });
 await page.setViewportSize({width:320,height:650});await page.goto('/panel.html');await expect(page.getByText('Perlen: 2000')).toBeVisible();
 await page.getByRole('button',{name:'Perks',exact:true}).click();await page.getByRole('button',{name:'Perk kaufen'}).click();
 await expect(page.getByText(/Aktion: Abgeschlossen/)).toBeVisible();await page.getByRole('button',{name:'Charakter',exact:true}).click();await expect(page.getByText('Perlen: 1500')).toBeVisible();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:'test-results/live-action-completed.png'});
});
test('offline and unauthenticated live panel blocks all mutations',async({page})=>{
 await page.route('https://extension-files.twitch.tv/**',route=>route.fulfill({contentType:'text/javascript',body:'window.Twitch={ext:{onAuthorized:cb=>cb({token:"synthetic",channelId:"42",userId:"Usynthetic"})}}'}));
 await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',route=>route.fulfill({json:route.request().url().endsWith('/state')?{available:false,reason:'stream_not_live',catalogJson:'{}'}:{rpg:{}}}));
 await page.setViewportSize({width:390,height:844});await page.goto('/mobile.html');await page.getByRole('button',{name:'Abenteuer',exact:true}).click();await expect(page.getByRole('button',{name:'Tauchgang starten'})).toBeDisabled();await expect(page.getByRole('button',{name:'Angel auswerfen'})).toBeDisabled();
});
