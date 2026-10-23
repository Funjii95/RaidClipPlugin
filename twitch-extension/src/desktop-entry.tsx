import {createRoot} from 'react-dom/client';
import {App} from './GraphicalApp';
import {DesktopAdmin} from './DesktopAdmin';
import {BackendRpgDataSource} from './remote/BackendRpgDataSource';
import {desktopTransport,type NativeBridge} from './desktop-transport';
import './desktop.css';
declare global {interface Window {
 otterDesktopIdentity?:{userId:string;displayName:string;channelId?:string;connected?:boolean;backendStatus?:string};
 chrome?:{webview:NativeBridge};
 otterFlush?:()=>void;
 otterCanRestart?:()=>boolean;
}}
const identity=window.otterDesktopIdentity;
const bridge=window.chrome?.webview;
const notice=document.getElementById('desktop-notice')!;
notice.textContent=`FUNJII OTTER RPG · ${identity?.displayName??'Nicht angemeldet'} · ${identity?.backendStatus??'Backend-Verbindung wird vorbereitet'}`;
try{
 if(!bridge)throw Error('Bitte Funjii Otter RPG über die Windows-Anwendung starten.');
 const source=new BackendRpgDataSource({fetch:desktopTransport(bridge),helper:{
  onAuthorized:callback=>{if(identity?.connected&&identity.channelId&&identity.userId!=='guest')callback({userId:identity.userId,channelId:identity.channelId,token:'native-session'});},
  actions:{requestIdShare:()=>bridge.postMessage({type:'login'})}
 }});
 source.start();window.otterFlush=()=>{};window.otterCanRestart=()=>source.getSnapshot().remote?.pending!==true;
 window.addEventListener('pagehide',()=>source.dispose());
 createRoot(document.getElementById('root')!).render(<><DesktopAdmin source={source} identity={identity}/><App source={source} minigameClient={source.minigames}/></>);
}catch(error){notice.textContent=error instanceof Error?error.message:'Backend-Verbindung konnte nicht gestartet werden.';notice.setAttribute('role','alert');}
