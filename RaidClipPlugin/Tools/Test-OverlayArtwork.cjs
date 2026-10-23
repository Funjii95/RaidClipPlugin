// Local, synthetic overlay rendering check. Does not contact OBS, Twitch, or real player data.
// Run: node Tools/Test-OverlayArtwork.cjs [absolute-output-directory]
// PLAYWRIGHT_MODULE may point to an installed Playwright package.
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const root=path.resolve(__dirname,'..'),out=path.resolve(process.argv[2]||path.join(root,'..','output','overlay-artwork'));
 fs.mkdirSync(out,{recursive:true});
 const source=fs.readFileSync(path.join(root,'Services','HeistOverlayPage.cs'),'utf8');
 const html=source.split('"""')[1];
 const png=fs.readFileSync(path.join(root,'Assets','Overlay','otter-events.png'));
 let kind=2,phase='Joining',longNames=false,offline=false,empty=false,mimic=false,deadline=0;
 const errors=[],requests=[];
 const server=http.createServer((req,res)=>{
  requests.push(req.url);
  if(req.url==='/assets/mimic.png'){res.setHeader('Content-Type','image/png');return res.end(fs.readFileSync(path.join(root,'Assets','Overlay','mimic.png')))}
  if(req.url==='/assets/otter-events.png'){res.setHeader('Content-Type','image/png');return res.end(png)}
  res.setHeader('Content-Type',req.url==='/state'||req.url==='/community'?'application/json':'text/html');
  const current=new Date(),end=new Date(deadline||+current+45000).toISOString();
  if(req.url==='/state')return res.end(JSON.stringify({BossActive:kind===0&&!empty,BossIsMimic:mimic,BossName:longNames?"Mimic <script>ungefährlicher Name</script>":"Mimic – Lebende Schatztruhe",BossLegendaryChance:50,BossHp:720,BossMaxHp:1000,BossPlayers:8,BossEndsAt:end,RacePlayers:6,RaceStake:100,RaceEndsAt:end}));
  if(req.url==='/community'){
   if(offline){res.statusCode=503;return res.end('{}')}
   const players=Array.from({length:longNames?24:4},(_,i)=>({Name:longNames?'EinSehrLangerOtterSpielername'+i:['Funjii','Otterpfote','Perlentaucher','KleineKrabbe'][i],Payout:1200}));
   return res.end(JSON.stringify({ServerNow:current.toISOString(),Current:empty?null:{Type:kind},Heist:{EventId:'synthetic',Name:'Überfall auf die Perlenschatzkammer',Phase:phase,StartedAt:current.toISOString(),JoinEndsAt:end,JoinCommand:'!joinheist',Currency:'Perlen',Jackpot:4800,EntryStake:0,ParticipantCount:players.length,Players:players,TotalPayout:4800,TotalLost:0,AnimationSeconds:6,ResultSeconds:10,SuccessAnimation:true,FailureAnimation:true},HotTimeActive:false}));
  }
  res.end(html);
 });
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const address='http://127.0.0.1:'+server.address().port;
 let browser;
 try{
  browser=await chromium.launch({headless:true,...(process.env.OVERLAY_CHROMIUM?{executablePath:process.env.OVERLAY_CHROMIUM}:{})});
  const page=await browser.newPage();
  page.on('pageerror',err=>errors.push(err.message));
  let checks=0;
  for(const size of [{width:900,height:600},{width:600,height:500}]){
   await page.setViewportSize(size);
   for(const [type,name]of [[0,'boss'],[0,'mimic'],[1,'race'],[2,'heist']]){
    kind=type;mimic=name==='mimic';phase='Joining';empty=false;offline=false;const panelId=mimic?'boss':name;
    await page.goto(address);await page.locator('#'+panelId+':not(.hidden)').waitFor();await page.waitForTimeout(600);
    const details=await page.locator('#'+panelId).evaluate(el=>{
     const b=el.getBoundingClientRect(),art=el.querySelector('.art').getBoundingClientRect(),svg=el.querySelector('svg');
     return {right:b.right,bottom:b.bottom,left:b.left,top:b.top,artWidth:art.width,viewBox:svg.getAttribute('viewBox'),transparent:getComputedStyle(document.body).backgroundColor};
    });
    assert(details.right<=size.width && details.bottom<=size.height,'Overlay clipped: '+JSON.stringify({size,name,details}));
    assert(details.transparent==='rgba(0, 0, 0, 0)');
    assert(details.artWidth>200);
    if(mimic){assert.equal(await page.locator('#mimicArt').evaluate(img=>img.naturalWidth),1254);assert.match(await page.locator('#mimicLoot').textContent(),/50 %/);}
    await page.screenshot({path:path.join(out,name+'-'+size.width+'x'+size.height+'.png'),omitBackground:true});
    checks++;
   }
  }
  await page.setViewportSize({width:600,height:500});kind=2;longNames=true;
  for(const state of ['Joining','Raiding','Successful','Failed','Cancelled']){
   phase=state;await page.goto(address);await page.locator('#heist:not(.hidden)').waitFor();await page.waitForTimeout(600);
   const bottom=await page.locator('#heist').evaluate(el=>el.getBoundingClientRect().bottom);
   assert(bottom<=500,'Heist phase clipped: '+state+' bottom='+bottom);
   checks++;
  }
  await page.screenshot({path:path.join(out,'heist-long-names.png'),omitBackground:true});
  empty=true;await page.waitForTimeout(800);
  assert.equal(await page.locator('.panel:not(.hidden)').count(),0);
  empty=false;offline=true;await page.waitForTimeout(800);
  assert.equal(await page.locator('.panel:not(.hidden)').count(),0);
  offline=false;await page.goto(address+'/preview');await page.getByRole('button',{name:'Boss anzeigen',exact:true}).click();
  await page.locator('#boss:not(.hidden)').waitFor();
  await page.getByRole('button',{name:'Rennen anzeigen',exact:true}).click();
  await page.locator('#race:not(.hidden)').waitFor();
  await page.getByRole('button',{name:'Mimic anzeigen',exact:true}).click();await page.locator('#boss.mimic:not(.hidden)').waitFor();
  kind=0;mimic=true;empty=false;longNames=false;deadline=Date.now()+45000;
  await page.goto(address+'/mimic');await page.locator('#boss.mimic:not(.hidden)').waitFor();
  assert.equal(await page.locator('#health').getAttribute('aria-label'),'Boss-Lebenspunkte');
  assert.equal(await page.locator('#health').evaluate(p=>p.value),720);
  await page.waitForTimeout(1200);await page.reload();await page.locator('#boss.mimic:not(.hidden)').waitFor();
  assert(!((await page.locator('#bossTime').textContent()).includes('0:45')),'Countdown restarted on reload');
  longNames=true;await page.waitForTimeout(700);assert.match(await page.locator('#bossTitle').textContent(),/<script>/);assert.equal(await page.locator('#bossTitle script').count(),0);
  mimic=false;await page.waitForTimeout(700);assert.equal(await page.locator('.panel:not(.hidden)').count(),0);
  mimic=true;deadline=Date.now()-1000;await page.waitForTimeout(700);assert.equal(await page.locator('.panel:not(.hidden)').count(),0);
  deadline=Date.now()+45000;offline=true;await page.waitForTimeout(700);assert.equal(await page.locator('.panel:not(.hidden)').count(),0);
  assert(requests.includes('/assets/mimic.png'));checks+=7;
  assert(requests.includes('/assets/otter-events.png'));
  assert.deepEqual(errors,[]);
  console.log(JSON.stringify({checks:checks+4,screenshots:out,errors},null,2));
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve))}
})().catch(err=>{console.error(err);process.exitCode=1});

