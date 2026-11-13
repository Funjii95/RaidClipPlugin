// Optional online probe of the unmodified public TIDAL Embed in a fresh anonymous browser.
// No account, token, private API, Spotify process, OBS connection or channel points are used.
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,...(process.env.TIDAL_CHROMIUM?{executablePath:process.env.TIDAL_CHROMIUM}:{})});
 try{
  const context=await browser.newContext();
  const page=await context.newPage();
  const response=await page.goto('https://embed.tidal.com/tracks/37936045',{waitUntil:'domcontentloaded',timeout:30000});
  await page.waitForTimeout(3500);
  const buttons=await page.getByRole('button').allTextContents();
  const labelled=page.getByRole('button',{name:/^(play|abspielen|wiedergabe)(\s|$)/i}).first();
  // The current public Embed exposes a single icon-only play button without an accessible name.
  const play=await labelled.count()?labelled:await page.getByRole('button').count()===1?page.getByRole('button').first():labelled;
  let clicked=false;
  if(await play.count()&&await play.isVisible()){await play.click();clicked=true;await page.waitForTimeout(4000)}
  const media=await page.locator('audio,video').evaluateAll(items=>items.map(item=>({duration:Number.isFinite(item.duration)?item.duration:null,currentTime:item.currentTime,paused:item.paused,error:item.error?.code??null})));
  console.log(JSON.stringify({httpStatus:response.status(),playButtonClicked:clicked,buttons:buttons.slice(0,12),media,note:'Anonymous browser probe only; this does not prove full-track playback or WebView2 account login.'},null,2));
 }finally{await browser.close()}
})().catch(error=>{console.error(error.name+': '+error.message);process.exitCode=1});
