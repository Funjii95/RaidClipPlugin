import {test,expect} from '@playwright/test';
import {authorization,profile,availability} from './remote-fixtures';
for(const [view,width,height] of [['panel',320,500],['mobile',390,844]] as const){
 test(`${view}: self deletion needs explicit confirmation and stays pending until plugin acknowledgement`,async({page})=>{
  let job:any=null;const posts:any[]=[];
  await page.clock.install();
  await page.route('https://extension-files.twitch.tv/**',r=>r.fulfill({contentType:'text/javascript',body:`window.Twitch={ext:{onAuthorized:cb=>cb(${JSON.stringify(authorization)})}};`}));
  await page.route('https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app/**',r=>{
   const req=r.request(),url=req.url();
   if(url.endsWith('/privacy/deletion')){
    expect(req.headers().authorization).toBe('Bearer '+authorization.token);
    if(req.method()==='GET')return r.fulfill({json:{request:job}});
    const body=req.postDataJSON();posts.push(body);expect(Object.keys(body).sort()).toEqual(['confirmation','requestId']);expect(body.confirmation).toBe('DELETE_MY_RPG_ACCOUNT');
    job??={userId:'1001',requestId:body.requestId,requestedAt:new Date().toISOString(),completed:false};return r.fulfill({status:202,json:job});
   }
   if(url.endsWith('/me'))return job?.completed?r.fulfill({status:404,json:{error:'profile_missing'}}):r.fulfill({json:profile()});
   if(url.endsWith('/state'))return r.fulfill({json:{...availability(),selfDeletionEnabled:true}});
   return r.fulfill({status:404,json:{error:'not_found'}});
  });
  await page.setViewportSize({width,height});await page.goto('/'+view+'.html');
  await page.getByText('Datenschutz · Meinen RPG-Spielstand löschen',{exact:true}).click();
  await page.getByRole('button',{name:'Löschung vorbereiten',exact:true}).click();
  const submit=page.getByRole('button',{name:'Endgültige Löschung beantragen',exact:true});await expect(submit).toBeDisabled();expect(posts).toHaveLength(0);
  await page.getByLabel('Ich möchte meinen gesamten RPG-Spielstand löschen.').check();await expect(submit).toBeDisabled();
  await page.getByLabel('Zur Bestätigung LÖSCHEN eingeben').fill('LÖSCHEN');await submit.click();
  await expect(page.getByText(/Löschung beantragt\. Wartet/)).toBeVisible();expect(posts).toHaveLength(1);await expect(page.getByText(/Noch nicht vollständig gelöscht/)).toBeVisible();
  await page.reload();await expect(page.getByText(/Löschung beantragt\. Wartet/)).toBeVisible();expect(posts).toHaveLength(1);
  job.completed=true;await page.clock.fastForward(6000);
  await expect(page.getByText('RPG-Spielstand im Plugin und in der Cloud gelöscht. Dein Twitch-Konto bleibt bestehen.')).toBeVisible();expect(posts).toHaveLength(1);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await page.screenshot({path:`test-results/self-deletion-${view}.png`});
 });
}
