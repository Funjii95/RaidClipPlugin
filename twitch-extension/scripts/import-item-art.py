"""Import user-provided item art by verified SHA256/ID; preserve alpha in compact web assets.
Usage: python scripts/import-item-art.py <archive.zip>
Requires Pillow for this one-time asset preparation; not for running the extension.
"""
from pathlib import Path
from PIL import Image
import zipfile,csv,io,json,hashlib,re,sys
root=Path(__file__).resolve().parents[1]
archive=Path(sys.argv[1]).resolve()
public=root/'public'/'items';public.mkdir(exist_ok=True)
with zipfile.ZipFile(archive) as z:
    rows=list(csv.DictReader(io.StringIO(z.read('Item-Zuordnung.csv').decode('utf-8-sig')),delimiter=';'))
    images={hashlib.sha256(z.read(e)).hexdigest():e for e in z.infolist() if e.filename.startswith('Bilder/') and e.filename.endswith('.png')}
    assert len({r['Item-ID'] for r in rows})==len(rows),'Duplicate item ID'
    entries={}
    for row in rows:
        item_id=row['Item-ID']
        if not re.fullmatch(r'[a-z0-9_-]+',item_id): raise ValueError('Invalid item ID')
        entry=images.get(row['SHA256'])
        if entry is None: raise ValueError('Missing or mismatched source: '+item_id)
        im=Image.open(io.BytesIO(z.read(entry))).convert('RGBA')
        im.thumbnail((384,384),Image.Resampling.LANCZOS)
        dest=public/(item_id+'.webp')
        im.save(dest,'WEBP',quality=88,method=6,exact=True)
        entries[item_id]={'src':'./items/'+dest.name,'name':Path(entry.filename).stem,'sourceSha256':row['SHA256'],'assetSha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'width':im.width,'height':im.height}
    data={'sourceArchive':archive.name,'artVersion':'2.1.22','entries':entries}
    (root/'src'/'item-art.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'{len(entries)} verified images; total {sum(p.stat().st_size for p in public.glob("*.webp"))/1024/1024:.2f} MiB')
