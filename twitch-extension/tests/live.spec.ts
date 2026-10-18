import {test,expect,Page} from '@playwright/test';
import {profile,catalog,availability,authorization} from './remote-fixtures';
async function backend(page:Page,options:{offline?:boolean;unauthorized?:boolean;missing?:boolean;corrupt?:boolean}={}){
 let current=profile(),posts:any[]=[],receipts=new Map<string,any>();
 await page.route('https://extension-files.twitch.tv/**',route=>route.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
 await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',async route=>{
  const request=route.request(),url=request.url();
  if(options.unauthorized)return route.fulfill({status:401,body:''});
  if(url.endsWith('/me'))return route.fulfill({json:options.corrupt?{...current,source:'mock'}:current});
  if(url.endsWith('/state'))return route.fulfill(options.missing?{status:404,json:{error:'missing'}}:{json:{...availability(),available:!options.offline,reason:options.offline?'plugin_not_ready':'ready',streamLive:false}});
  if(request.method()==='POST'){
   const body=request.postDataJSON();expect(body.userId).toBeUndefined();expect(request.headers()['authorization']).toBe('Bearer '+authorization.token);
   expect(request.headers()['x-rpg-session']).toBe(availability().sessionId);
   posts.push(body);
   if(!receipts.has(body.actionId)){expect(body.kind).toBe('equip');expect(body.arguments).toEqual({instanceId:'owned-trident',slot:'Weapon'});current=profile(1234,true);receipts.set(body.actionId,{actionId:body.actionId,state:'applied',outcome:{success:true,code:'applied',message:'',dataJson:'{}'}});}
   return route.fulfill({status:202,json:{actionId:body.actionId,state:'queued'}});
  }
  return route.fulfill({json:receipts.get(url.split('/').pop()!)});
 });
 return {posts,options};
}
for(const [view,width,height] of [['panel',320,700],['mobile',390,844]] as const){
 test(`${view}: original graphical UI permits play with Twitch offline and shows authoritative inventory and equipment, safely acts, survives reload`,async({page})=>{
  const server=await backend(page);await page.setViewportSize({width,height});await page.goto('/'+view+'.html');
  await expect(page.locator('.statusbar')).toContainText('1.234');await expect(page.getByRole('heading',{name:'SYNTHETIC Twitch-Testotter'})).toBeVisible();
  await expect(page.getByRole('heading',{name:'Level 7',exact:true})).toBeVisible();await expect(page.locator('.health-label')).toContainText('87 / 150 HP');
  await expect(page.locator('header img')).toHaveAttribute('src','./otter-mage.png');expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  expect(await page.locator('body').innerText()).not.toContain('Demo-Perlen');expect(await page.locator('body').innerText()).not.toContain('NaN');
  for(const tab of ['Abenteuer','Inventar','Ausrüstung','Itemsets','Perks','Boss','Kochen']){await page.getByRole('button',{name:tab,exact:true}).click();await expect(page.getByRole('heading',{name:tab,exact:true})).toBeVisible();}
  await page.getByRole('button',{name:'Abenteuer',exact:true}).click();await expect(page.locator('.sea-art img')).toHaveAttribute('src','./luminous-depths.png');
  await page.getByRole('button',{name:'Inventar',exact:true}).click();const card=page.locator('[data-instance-id="owned-trident"]');await expect(card.getByRole('heading',{name:'Gespeicherter Dreizack'})).toBeVisible();
  await expect(card.locator('img')).toHaveAttribute('src','./items/trident.webp');await expect(card).toContainText('12.5');
  await expect(card.getByRole('button',{name:'☆ Favorisieren'})).toHaveCount(0);await expect(card.getByRole('button',{name:'★ Favorisiert'})).toBeDisabled();
  const button=card.getByRole('button',{name:'Ausrüsten',exact:true});await button.evaluate(b=>{(b as HTMLButtonElement).click();(b as HTMLButtonElement).click();});
  await expect(card.getByRole('button',{name:'Ablegen',exact:true})).toBeVisible();expect(server.posts).toHaveLength(1);
  await page.getByRole('button',{name:'Ausrüstung',exact:true}).click();await expect(page.locator('.equipment').filter({has:page.getByRole('heading',{name:'Weapon',exact:true})})).toContainText('Gespeicherter Dreizack');
  await page.reload();await expect(page.locator('.statusbar')).toContainText('1.234');await page.getByRole('button',{name:'Ausrüstung',exact:true}).click();await expect(page.getByRole('heading',{name:'Gespeicherter Dreizack'})).toBeVisible();expect(server.posts).toHaveLength(1);
  await page.getByRole('button',{name:'Charakter',exact:true}).click();await page.screenshot({path:`test-results/graphical-${view}.png`});
 });
}
test('offline plugin and disconnected routes lock all mutations while inventory remains readable',async({page})=>{
 const state=await backend(page,{offline:true});await page.goto('/panel.html');await expect(page.locator('.statusbar')).toContainText('offline');
 await page.getByRole('button',{name:'Inventar',exact:true}).click();await expect(page.getByRole('heading',{name:'Gespeicherter Dreizack'})).toBeVisible();await expect(page.getByRole('button',{name:'Ausrüsten',exact:true})).toBeDisabled();
 await page.getByRole('button',{name:'Abenteuer',exact:true}).click();await expect(page.getByRole('button',{name:'Tauchgang starten'})).toBeDisabled();await expect(page.getByRole('button',{name:'Angel auswerfen'})).toBeDisabled();
 state.options.missing=true;state.options.offline=false;await page.reload();await expect(page.locator('.statusbar')).toContainText('noch nicht verfügbar');await expect(page.locator('.statusbar')).toContainText('1.234');expect(state.posts).toHaveLength(0);
});
test('JWT errors show identity permission and never demo data, including config.html',async({page})=>{
 await backend(page,{unauthorized:true});await page.goto('/mobile.html');await expect(page.getByRole('button',{name:'Twitch-Identität freigeben'})).toBeVisible();await expect(page.locator('.statusbar')).toContainText('abgelehnt');
 expect(await page.locator('body').innerText()).not.toContain('25.000');await expect(page.locator('[data-instance-id^="demo-"]')).toHaveCount(0);
 await page.goto('/config.html');await expect(page.getByRole('heading',{name:'Extension-Konfiguration'})).toBeVisible();expect(await page.locator('body').innerText()).not.toContain('Demo-Konfiguration');
});
test('production URL parameters cannot activate a mock fallback and mock backend payload is rejected',async({page})=>{
 await backend(page,{corrupt:true});await page.goto('/panel.html?demo=1');await expect(page.locator('.statusbar')).toContainText('keinen echten');await expect(page.locator('[data-instance-id]')).toHaveCount(0);expect(await page.locator('body').innerText()).not.toContain('25.000');
});
