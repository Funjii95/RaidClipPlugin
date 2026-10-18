import {createRoot} from 'react-dom/client';
import {App} from './GraphicalApp';
import {MockRpgDataSource,characterStats,collectors,diveInfo,setCount,perkCost,hotActive} from './mock';
import {sets,recipes} from './data';
import {MockMinigameClient} from './minigames/mock-client';
const source=new MockRpgDataSource();
const minigameClient=new MockMinigameClient({balance:()=>source.getSnapshot().player.pearls,settle:(id,stake,payout)=>source.settleMinigameFixture(id,stake,payout)});
createRoot(document.getElementById('root')!).render(<App source={source} minigameClient={minigameClient} demo={{sets,recipes,characterStats,collectors,diveInfo,setCount,perkCost,hotActive}}/>);
