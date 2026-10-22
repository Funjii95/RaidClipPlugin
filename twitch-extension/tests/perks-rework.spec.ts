import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';
test('perk categories, search, ranks and affordability use the saved catalog',async({page})=>{
 const rules:any=catalog();rules.perks=[{Id:'damage',Name:'Starke Pfoten',MaximumLevel:5,BonusPerLevel:{Damage:2},Costs:['500','1000','2000']},{Id:'fishing',Name:'Ruhige Hand',MaximumLevel:3,BonusPerLevel:{FishingLuck:4},Costs:['500']},{Id:'dive',Name:'Tiefentaucher',MaximumLevel:3,BonusPerLevel:{DiveLuck:3},Costs:['500']}];
 await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
 await page.route(/^https:\/\/(?:rc1---)?funjii-otter-rpg-backend-test-cwzl75ajla-ey\.a\.run\.app\//,r=>r.fulfill({json:r.request().url().endsWith('/me')?profile():{available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(rules)}}));
 await page.setViewportSize({width:1280,height:1000});await page.goto('/panel.html');await page.getByRole('button',{name:'Perks',exact:true}).click();
 await expect(page.locator('.perk-card')).toHaveCount(3);await expect(page.getByRole('button',{name:'Aufwerten · 2.000 Perlen',exact:true})).toBeDisabled();
 await expect(page.locator('.perk-detail')).toContainText('+4 % Schadensbonus');
 await page.getByRole('button',{name:/Angeln/}).click();await expect(page.locator('.perk-card')).toHaveCount(1);await expect(page.locator('.perk-detail h3')).toHaveText('Ruhige Hand');
 await page.getByRole('button',{name:/Alle Perks/}).click();await page.getByPlaceholder('Perks suchen …').fill('Tiefentaucher');await expect(page.locator('.perk-card')).toHaveCount(1);
 await page.getByPlaceholder('Perks suchen …').fill('');await page.screenshot({path:'test-results/perks-desktop.png',fullPage:true});
 for(const width of [320,390]){await page.setViewportSize({width,height:800});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);}
 await page.screenshot({path:'test-results/perks-mobile.png',fullPage:true});
});

test('narrow panel buys directly from its card without scrolling through the perk list',async({page})=>{
 const rules:any=catalog();rules.perks=Array.from({length:24},(_,i)=>({Id:'perk-'+i,Name:'Testperk '+i,MaximumLevel:5,BonusPerLevel:{Damage:2},Costs:['500','1000','2000','4000','8000']}));
 let purchased='';
 await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
 await page.route(/^https:\/\/(?:rc1---)?funjii-otter-rpg-backend-test-cwzl75ajla-ey\.a\.run\.app\//,r=>{
  const url=r.request().url();
  if(url.endsWith('/me'))return r.fulfill({json:profile(20000)});
  if(url.endsWith('/state'))return r.fulfill({json:{available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(rules)}});
  const intent=r.request().postDataJSON();expect(intent.kind).toBe('buyPerk');purchased=intent.arguments.perkId;
  return r.fulfill({status:202,json:{actionId:intent.actionId,state:'applied',outcome:{success:true,dataJson:JSON.stringify({Balance:19500,profile:profile(19500)})}}});
 });
 await page.setViewportSize({width:320,height:600});await page.goto('/panel.html');await page.getByRole('button',{name:'Perks',exact:true}).click();
 const card=page.getByRole('article',{name:'Testperk 1',exact:true});
 const buy=card.getByRole('button',{name:'Aufwerten · 500 Perlen',exact:true});
 await buy.scrollIntoViewIfNeeded();await expect(buy).toBeInViewport();
 const cardBox=await card.boundingBox(),buttonBox=await buy.boundingBox();
 expect(buttonBox!.y).toBeGreaterThanOrEqual(cardBox!.y);expect(buttonBox!.y+buttonBox!.height).toBeLessThanOrEqual(cardBox!.y+cardBox!.height+1);
 await expect(page.locator('.perk-detail')).not.toBeInViewport();
 await buy.click();await expect.poll(()=>purchased).toBe('perk-1');
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.screenshot({path:'test-results/perk-inline-upgrade.png'});
});
