import {test,expect} from '@playwright/test';
import {profile,catalog,authorization} from './remote-fixtures';

for(const [view,width,height] of [['panel',320,500],['mobile',390,844]] as const){
 test(`${view}: boss, protected inventory and shared race use server responses`,async({page})=>{
  let current=profile();const posts:any[]=[];let race=false,version=0;
  const games=['gamble','coinflip','slots','roulette','heist','race','duel'];
  const frame=()=>({rules:games.map(id=>({id,name:id==='race'?'Otter-Rennen':id,enabled:true,canStart:id==='race',canJoin:['race','heist'].includes(id),usesStake:id!=='heist',minimumStake:id==='heist'?0:1,maximumStake:id==='heist'?0:10000,cooldownSeconds:0,joinSeconds:60,minimumParticipants:1,maximumParticipants:50,startCommand:'',joinCommand:'',choices:[],description:'Synthetischer Serverstatus'})),frame:{streamId:'synthetic-session',revision:++version,serverTime:Date.now(),balance:current.points,states:games.map(game=>({game,name:game,eventId:game==='race'&&race?'synthetic-race':'-',version,phase:game==='race'&&race?'joining':'inactive',joinEndsAt:Date.now()+60000,participants:[],participantCount:game==='race'&&race?1:0,joined:game==='race'&&race,personalStake:game==='race'&&race?100:0,requiredStake:100}))}});
  const boss={status:{BossActive:true,BossHp:900,BossMaxHp:1000,BossEventId:'synthetic-boss',BossEndsAt:new Date(Date.now()+60000).toISOString()},damage:100};
  await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)}),actions:{requestIdShare:()=>{}}}};`}));
  await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',async r=>{
   const req=r.request(),url=req.url();if(url.endsWith('/me'))return r.fulfill({json:current});
   if(url.endsWith('/state'))return r.fulfill({json:{available:true,sessionId:'11111111-1111-4111-8111-111111111111',streamLive:false,catalogJson:JSON.stringify({...catalog(),supported:[...catalog().supported,'status','bossStatus','bossAttack','favorite','itemlock','salvagePreview','salvageConfirm','minigames','casino','community']})}});
   const intent=req.postDataJSON();posts.push(intent);expect(intent.userId).toBeUndefined();expect(req.headers().authorization).toBe('Bearer '+authorization.token);let data:any={};
   if(intent.kind==='status')data={profile:current,stats:{}};
   if(intent.kind==='bossStatus')data=boss;
   if(intent.kind==='bossAttack'){boss.status.BossHp=800;boss.damage=200;data={boss};}
   if(intent.kind==='favorite')current.rpg.inventory[0].IsFavorite=false;
   if(intent.kind==='minigames')data=frame();
   if(intent.kind==='community'){expect(intent.arguments).toMatchObject({game:'race',operation:'start',eventId:'-'});race=true;data=frame();}
   return r.fulfill({status:202,json:{actionId:intent.actionId,state:'applied',outcome:{success:true,code:'applied',message:'',dataJson:JSON.stringify(data)}}});
  });
  const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));
  await page.setViewportSize({width,height});await page.goto('/'+view+'.html');
  await expect(page.locator('.statusbar')).toContainText('Plugin online');
  await page.getByRole('button',{name:'Boss',exact:true}).click();await expect(page.getByRole('button',{name:/Boss angreifen/})).toBeEnabled();
  await page.getByRole('button',{name:/Boss angreifen/}).click();await expect(page.locator('progress[aria-label="Boss HP"]')).toHaveAttribute('value','800');
  await page.screenshot({path:`test-results/extended-${view}-boss.png`});
  await page.getByRole('button',{name:'Inventar',exact:true}).click();await page.getByRole('button',{name:'★ Favorisiert',exact:true}).click();await expect(page.getByRole('button',{name:'☆ Favorisieren',exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Perlen-Minispiele',exact:true}).click();await page.getByRole('button',{name:'Otter-Rennen',exact:true}).click();
  await page.getByRole('button',{name:'Rennen starten',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Bestätigen',exact:true}).click();
  await expect(page.getByRole('button',{name:'Bereits angemeldet',exact:true})).toBeVisible();
  expect(posts.filter(p=>p.kind==='bossAttack')).toHaveLength(1);expect(posts.filter(p=>p.kind==='community')).toHaveLength(1);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);expect(errors).toEqual([]);
  await page.screenshot({path:`test-results/extended-${view}-race.png`});
 });
}
