import {useState} from 'react';
import artwork from './item-art.json';
import tidesArtwork from './tides-art.json';
import depthsArtwork from './depths-art.json';
type Entry={src:string;name:string;width:number;height:number;sourceSha256:string;assetSha256:string};
export const itemArt:Readonly<Record<string,Entry>>={...artwork.entries,...tidesArtwork,...depthsArtwork};
export function ItemArtwork({id,name,fallback='✧',small=false}:{id:string;name?:string;fallback?:string;small?:boolean}){
 const [failedId,setFailedId]=useState<string>();const entry=itemArt[id];
 if(!entry||failedId===id)return <span className="item-art-fallback" aria-hidden="true">{fallback}</span>;
 return <img className={small?'item-art item-art-small':'item-art'} src={entry.src} alt={name??entry.name} width={entry.width} height={entry.height} loading="lazy" decoding="async" onError={()=>setFailedId(id)}/>;
}
