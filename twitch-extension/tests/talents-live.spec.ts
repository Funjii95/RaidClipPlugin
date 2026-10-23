import {test,expect} from '@playwright/test';
import {authorization,profile,catalog} from './remote-fixtures';
import talentCatalog from '../../Sync/src/SnapshotProtocol/talents.json' with {type:'json'};
test('confirmed allocation updates points and persists across a reload',async({page})=>{
 let rank=0,requests=0;const updated=()=>({...profile(),updatedAt:rank?'2026-10-21T12:00:01Z':'2026-10-21T12:00:00Z',rpg:{...profile().rpg,playerLevel:30,talentRanks:rank?{'combat-power':rank}:{}}});
 await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)})}};`}));
 await page.route(/^https:\/\/(?:rc1---)?funjii-otter-rpg-backend-test-cwzl75ajla-ey\.a\.run\.app\//,async r=>{
  if(r.request().url().endsWith('/me'))return r.fulfill({json:updated()});
  if(r.request().url().endsWith('/state'))return r.fulfill({json:{available:true,sessionId:'11111111-1111-4111-8111-111111111111',catalogJson:JSON.stringify({...catalog(),supported:['learnTalent'],talentCatalogVersion:1,talents:talentCatalog})}});
  const intent=r.request().postDataJSON();expect(intent.kind).toBe('learnTalent');expect(intent.arguments).toEqual({talentId:'combat-power',expectedRank:String(rank),catalogVersion:'1'});rank++;requests++;
  return r.fulfill({status:202,json:{actionId:intent.actionId,state:'applied',outcome:{success:true,code:'applied',message:'Talent gespeichert.',dataJson:JSON.stringify({profile:updated()})}}});
 });
 await page.goto('/panel.html');await page.getByRole('button',{name:'Talente',exact:false}).click();
 const tree=page.getByRole('region',{name:'Talentbaum'});const points=tree.getByRole('status',{name:'Verbleibende Talentpunkte'});
 await expect(points).toContainText('29');await tree.getByRole('button',{name:'Kräftiger Schlag: Lernen · 1 Talentpunkt',exact:true}).click();
 await expect(points).toContainText('28');expect(requests).toBe(1);
 await page.reload();await page.getByRole('button',{name:'Talente',exact:false}).click();await expect(points).toContainText('28');
 await expect(tree.locator('.talent-node').first()).toContainText('1 / 5');
 const bg=await page.request.get('/talent-scenes.png');expect(bg.ok()).toBeTruthy();
 await tree.scrollIntoViewIfNeeded();await page.screenshot({path:'test-results/talents-combat-new.png',fullPage:true});
 await tree.getByRole('button',{name:/Angeln/}).click();await page.screenshot({path:'test-results/talents-fishing-new.png',fullPage:true});
 await tree.getByRole('button',{name:/Tauchen/}).click();await page.screenshot({path:'test-results/talents-diving-new.png',fullPage:true});
});
