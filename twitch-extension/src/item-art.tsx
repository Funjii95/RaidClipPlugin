import starterArtwork from './starter-art.json';
import {useState} from 'react';
import artwork from './item-art.json';
import tidesArtwork from './tides-art.json';
import depthsArtwork from './depths-art.json';
import eternalArtwork from './eternal-art.json';
import cuisineArtwork from './cuisine-art.json';
import weaponArtwork from './weapons-art.json';
import eternalEightArtwork from './eternal-eight-art.json';
type Entry={src:string;name:string;width:number;height:number;sourceSha256:string;assetSha256:string};
export const itemArt:Readonly<Record<string,Entry>>={...starterArtwork,...artwork.entries,...tidesArtwork,...depthsArtwork,...eternalArtwork,...cuisineArtwork,...weaponArtwork,...eternalEightArtwork};
export function ItemArtwork({id,name,fallback='✧',small=false}:{id:string;name?:string;fallback?:string;small?:boolean}){
 const [failedId,setFailedId]=useState<string>();const questSlot=Number(id.match(/^quest_[a-z]+_(\d)$/)?.[1]);const aliases=["tide_king_helmet","tide_king_chest","tide_king_gloves","tide_king_boots","weapon_tide_crown","tidal-seal","leviathan-seal","deepsea-heart"];const entry=itemArt[id]??(id.startsWith("quest_")?itemArt[aliases[questSlot]]:undefined);
 if(!entry||failedId===id)return <span className="item-art-fallback" aria-hidden="true">{fallback}</span>;
 return <img className={small?'item-art item-art-small':'item-art'} src={entry.src} alt={name??entry.name} width={entry.width} height={entry.height} loading="lazy" decoding="async" onError={()=>setFailedId(id)}/>;
}
