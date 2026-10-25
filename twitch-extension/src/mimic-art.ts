import artwork from './mimic-art.json';
type MimicArtwork={src:string;name:string;width:number;height:number;sourceSha256:string;assetSha256:string};
export const mimicArt:Readonly<Record<string,MimicArtwork>>=artwork;
export function mimicImage(zoneId?:string):string{
 return (zoneId?mimicArt[zoneId]?.src:undefined)??'./boss-mimic.png';
}
