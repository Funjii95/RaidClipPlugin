import {createRoot} from 'react-dom/client';
import {App} from './GraphicalApp';
import {BackendRpgDataSource} from './remote/BackendRpgDataSource';
if(import.meta.env.MODE==='demo') {
 void import('./demo-entry');
} else {
 const helper=(window as unknown as {Twitch?:{ext?:import('./remote/BackendRpgDataSource').TwitchHelper}}).Twitch?.ext;
 const source=new BackendRpgDataSource({helper});source.start();
 createRoot(document.getElementById('root')!).render(<App source={source}/>);
}
