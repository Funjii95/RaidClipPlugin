// Synthetic local browser tests; never connects to Twitch, OBS or real chat.
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const root=path.resolve(__dirname,'..'),out=path.resolve(process.argv[2]||'chat-overlay-preview');fs.mkdirSync(out,{recursive:true});
 const html=fs.readFileSync(path.join(root,'Services','ChatOverlayPage.cs'),'utf8').split('"""')[1];
 let offline=false,messages=[],requests=0;const errors=[];
 const message=(id,text='Hallo Otter! 🦦',extra={})=>({Id:String(id),Timestamp:new Date().toISOString(),Name:'Otter'+id,Text:text,Color:'#7ce0e6',Emotes:[],...extra});
 const server=http.createServer((req,res)=>{if(req.url==='/state'){requests++;res.setHeader('Content-Type','application/json');res.statusCode=offline?503:200;res.end(JSON.stringify({ServerNow:new Date().toISOString(),Messages:messages,ShowUserColors:true,ShowBadges:true,ShowTimestamps:false,EmoteSize:32}));}else{res.setHeader('Content-Type','text/html');res.end(html);}});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));const address='http://127.0.0.1:'+server.address().port;let browser;let checks=0;
 try{
  browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:500,height:700}});page.on('pageerror',e=>errors.push(e.message));
  messages=[message(1),message(2,'Die Mimic hat heute richtig Beute dabei! 💎',{Name:'Perlentaucher',Subscriber:true}),message(3,'Bereit für das nächste Abenteuer? 🌊',{Name:'Funjii95',Color:'#ffcb76',Broadcaster:true})];
  await page.goto(address);await page.waitForFunction(()=>document.querySelectorAll('.message').length===3);
  assert.equal(await page.evaluate(()=>getComputedStyle(document.body).backgroundColor),'rgba(0, 0, 0, 0)');checks++;
  assert.equal(await page.locator('#preview').isVisible(),false);checks++;
  await page.screenshot({path:path.join(out,'Chat-Overlay-500x700.png'),omitBackground:true});
  assert.equal(await page.locator('.name').last().evaluate(n=>getComputedStyle(n).color),'rgb(255, 203, 118)');checks++;
  const before=await page.locator('.message').first().evaluate(n=>{n.dataset.preserved='yes';return true});await page.waitForTimeout(650);assert.equal(await page.locator('.message').first().getAttribute('data-preserved'),'yes');checks++;
  messages=[message(4,'<img src=x onerror="window.pwned=1">',{Name:'<script>unsafe</script>',Color:'red;display:none'})];
  await page.waitForFunction(()=>document.querySelector('.message')?.dataset.id==='4');assert.equal(await page.evaluate(()=>window.pwned),undefined);assert.equal(await page.locator('script[src],img').count(),0);checks+=2;
  assert.ok((await page.locator('.message').innerText()).includes('<img'));checks++;
  messages=Array.from({length:40},(_,i)=>message(i));await page.waitForFunction(()=>document.querySelector('.message')?.dataset.id==='32');assert.equal(await page.locator('.message').count(),8);checks++;
  await page.goto(address+'/?count=3&font=32&fade=2');await page.waitForFunction(()=>document.querySelectorAll('.message').length===3);assert.equal(await page.evaluate(()=>getComputedStyle(document.body).fontSize),'32px');checks++;
  await page.waitForFunction(()=>document.querySelectorAll('.message').length===0);checks++;
  messages=[message(99,'Kappa Bad',{Emotes:[{Code:'Kappa',Url:'https://static-cdn.jtvnw.net/emoticons/v2/25/static/dark/2.0'},{Code:'Bad',Url:'https://evil.invalid/image'}]})];
  await page.route('https://static-cdn.jtvnw.net/**',route=>route.fulfill({status:404,body:''}));await page.goto(address);await page.waitForFunction(()=>document.querySelector('.message')?.textContent.includes('Kappa Bad'));assert.equal(await page.locator('img').count(),0);checks++;
  offline=true;await page.waitForFunction(()=>document.querySelectorAll('.message').length===0);checks++;
  offline=false;messages=[message(100,'Wieder verbunden')];await page.waitForFunction(()=>document.querySelector('.message')?.dataset.id==='100');checks++;
  messages=[message(101,'SehrLangerOttertext'.repeat(110))];await page.setViewportSize({width:320,height:400});await page.waitForFunction(()=>document.querySelector('.message')?.dataset.id==='101');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);checks++;
  const baseline=requests;await page.goto(address+'/preview');await page.waitForFunction(()=>document.querySelectorAll('.message').length===4);await page.waitForTimeout(600);assert.ok(requests<=baseline+1);checks++;
  await page.setViewportSize({width:500,height:700});await page.screenshot({path:path.join(out,'Chat-Vorschau-500x700.png'),omitBackground:true});
  assert.deepEqual(errors,[]);checks++;console.log(JSON.stringify({checks,errors,out}));
 }finally{await browser?.close();await new Promise(r=>server.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1});
