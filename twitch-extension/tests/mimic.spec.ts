import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';
test('Mimic uses supplied art and authoritative name and loot chance; perks show five ranks',async({page})=>{
 const saved:any=profile();saved.rpg.perks={'abyss-sense':5};
 const rules:any=catalog();rules.supported=['bossStatus','bossAttack','buyPerk'];rules.perks=[{Id:'abyss-sense',Name:'Abgrundsinn',MaximumLevel:5,BonusPerLevel:{},Costs:['500','1000','2000','4000','8000'],Presentation:{Category:'diving',Symbol:'♆',Value:.1,Label:'Prozentpunkte zusätzliche Mimic-Chance (plus Grundchance)'}}];
 await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
 await page.route(/^https:\/\/(?:rc1---)?funjii-otter-rpg-backend-test-cwzl75ajla-ey\.a\.run\.app\//,async r=>{
  const url=r.request().url();if(url.endsWith('/me'))return r.fulfill({json:saved});if(url.endsWith('/state'))return r.fulfill({json:{available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(rules)}});
  const intent=r.request().postDataJSON();expect(intent.kind).toBe('bossStatus');return r.fulfill({status:202,json:{actionId:intent.actionId,state:'applied',outcome:{success:true,dataJson:JSON.stringify({status:{BossActive:true,BossHp:200,BossMaxHp:200,BossEventId:'mimic-test',BossName:'Mimic – Lebende Schatztruhe',BossIsMimic:true,BossLegendaryChance:50,BossEndsAt:new Date(Date.now()+60000).toISOString()},damage:0})}}});
 });
 await page.setViewportSize({width:390,height:844});await page.goto('/panel.html');await page.getByRole('button',{name:'Boss',exact:true}).click();
 await expect(page.getByRole('heading',{name:'Mimic – Lebende Schatztruhe'})).toBeVisible();await expect(page.locator('.boss-art img')).toHaveAttribute('src','./boss-mimic.png');await expect(page.locator('.boss')).toContainText('50 %');
 await expect(page.locator('.boss-art img')).toHaveJSProperty('naturalWidth',1254);await page.screenshot({path:'test-results/mimic-mobile.png',fullPage:true});
 await page.getByRole('button',{name:'Perks',exact:true}).click();await page.getByRole('button',{name:/Tauchen/}).click();
 await expect(page.locator('.perk-card-rank')).toHaveText('5 / 5 · MAX');await expect(page.locator('.perk-detail')).toContainText('0,5 Prozentpunkte');await expect(page.getByRole('button',{name:'Maximallevel',exact:true})).toBeDisabled();await expect(page.locator('.perk-card img')).toHaveCount(0);
});
