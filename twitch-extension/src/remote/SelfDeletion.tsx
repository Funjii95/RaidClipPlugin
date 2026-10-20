import {useEffect,useRef,useState} from 'react';
import type {RpgDataSource} from '../types';
import type {SelfDeletionJob} from '../live/client';
import {LiveHTTPError} from '../live/client';

export function SelfDeletion({source}:{source:RpgDataSource}){
 const [job,setJob]=useState<SelfDeletionJob|null>(null),[available,setAvailable]=useState(false),[open,setOpen]=useState(false),[phrase,setPhrase]=useState(''),[checked,setChecked]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const lock=useRef(false),identity=source.getSnapshot().player.id;
 useEffect(()=>{setJob(null);setOpen(false);setPhrase('');setChecked(false);setAvailable(false);setError('');let alive=true,reading=false;
  const read=async()=>{if(reading||!source.selfDeletionStatus)return;reading=true;try{const value=await source.selfDeletionStatus();if(alive){setJob(value.request);setAvailable(true);setError('');}}catch(e){if(alive&&e instanceof LiveHTTPError&&[401,403,404,503].includes(e.status))setAvailable(false);}finally{reading=false;}};
  void read();const timer=setInterval(()=>void read(),5000);return()=>{alive=false;clearInterval(timer);};
 },[source,identity]);
 if(!source.requestSelfDeletion||!available)return null;
 async function submit(){if(lock.current||phrase!=='LÖSCHEN'||!checked)return;lock.current=true;setBusy(true);setError('');try{const value=await source.requestSelfDeletion!(crypto.randomUUID());setJob(value);setOpen(false);}catch{setError('Bestätigung noch unklar. Status wird erneut geprüft; bitte keinen anderen Auftrag starten.');}finally{lock.current=false;setBusy(false);}}
 return <details className="self-deletion" open={job?true:undefined}><summary>Datenschutz · Meinen RPG-Spielstand löschen</summary>
  {job?<p role="status">{job.completed?'RPG-Spielstand im Plugin und in der Cloud gelöscht. Dein Twitch-Konto bleibt bestehen.':'Löschung beantragt. Wartet auf das Plugin, einen sicheren Wartungspunkt und die bestätigte Cloud-Bereinigung. Noch nicht vollständig gelöscht.'}</p>:<>
   <p>Nur dein eigener RPG-Spielstand wird gelöscht: Perlen, Fortschritt, Gegenstände und weitere gespeicherte Spieldaten. Dein Twitch-Konto bleibt bestehen. Eine minimale Twitch-ID-Löschsperre bleibt gegen automatische Neuanlage erhalten. Externe Exporte und Betriebsprotokolle müssen gegebenenfalls separat geprüft werden.</p>
   <button type="button" onClick={()=>setOpen(true)}>Löschung vorbereiten</button>
   {open&&<div role="group" aria-label="RPG-Löschung bestätigen"><p>Die Löschung kann nach Ausführung nicht rückgängig gemacht werden. Bei offline geschaltetem oder spielendem Plugin bleibt der Auftrag ausstehend. Eine neue Spielteilnahme ist dann gesperrt.</p><label><input type="checkbox" checked={checked} onChange={e=>setChecked(e.target.checked)}/>Ich möchte meinen gesamten RPG-Spielstand löschen.</label><label>Zur Bestätigung LÖSCHEN eingeben<input value={phrase} onChange={e=>setPhrase(e.target.value)} autoComplete="off"/></label><button type="button" disabled={busy||!checked||phrase!=='LÖSCHEN'} onClick={()=>void submit()}>Endgültige Löschung beantragen</button><button type="button" disabled={busy} onClick={()=>setOpen(false)}>Abbrechen</button></div>}
  </>}{error&&<p role="alert">{error}</p>}
 </details>;
}
