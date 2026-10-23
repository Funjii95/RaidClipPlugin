import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';
test('perk categories, search, ranks and affordability use the saved catalog',async({page})=>{
 const rules:any=catalog();rules.perks=[{Id:'damage',Name:'Starke Pfoten',MaximumLevel:5,BonusPerLevel:{Damage:2},Costs:['500','1000','2000']},{Id:'fishing',Name:'Ruhige Hand',MaximumLevel:3,BonusPerLevel:{FishingLuck:4},Costs:['500']},{Id:'dive',Name:'Tiefentaucher',MaximumLevel:3,BonusPerLevel:{DiveLuck:3},Costs:['500']}];
 await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
 await page.route(/^https:\/\/(?:rc1---)?funjii-otter-rpg-backend-test-cwzl75ajla-ey\.a\.run\.app\//,r=>r.fulfill({json:r.request().url().endsWith('/me')?profile():{available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify(rules)}}));
 await page.setViewportSize({width:1280,height:1000});await page.goto('/panel.html');await page.getByRole('button',{name:'Perks',exact:true}).click();
 await expect(page.locator('.perk-card')).toHaveCount(3);await expect(page.getByRole('button',{name:'Kaufen · 2.000 Perlen',exact:true})).toBeDisabled();
 await expect(page.locator('.perk-detail')).toContainText('+4 % Schadensbonus');
 await page.getByRole('button',{name:/Angeln/}).click();await expect(page.locator('.perk-card')).toHaveCount(1);await expect(page.locator('.perk-detail h3')).toHaveText('Ruhige Hand');
 await page.getByRole('button',{name:/Alle Perks/}).click();await page.getByPlaceholder('Perks suchen …').fill('Tiefentaucher');await expect(page.locator('.perk-card')).toHaveCount(1);
 await page.getByPlaceholder('Perks suchen …').fill('');await page.screenshot({path:'test-results/perks-desktop.png',fullPage:true});
 for(const width of [320,390]){await page.setViewportSize({width,height:800});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);}
 await page.screenshot({path:'test-results/perks-mobile.png',fullPage:true});
});
