"""Package built Twitch assets with smaller WebP illustrations; original assets stay intact."""
from pathlib import Path
from PIL import Image
import io,json,zipfile,sys,hashlib
source=Path(__file__).resolve().parent.parent/'dist'
output=Path(sys.argv[1]).resolve();output.parent.mkdir(parents=True,exist_ok=True)
images={p.relative_to(source).as_posix():p.relative_to(source).with_suffix('.hosted.webp').as_posix() for p in source.rglob('*.png')}
manifest=[]
with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as archive:
 for p in source.rglob('*'):
  if not p.is_file():continue
  name=p.relative_to(source).as_posix();data=p.read_bytes()
  if name in images:
   original=data
   with Image.open(p) as image:
    image.thumbnail((1024,1024),Image.Resampling.LANCZOS)
    buffer=io.BytesIO();image.save(buffer,format='WEBP',quality=90,method=6);data=buffer.getvalue()
   manifest.append({'source':name,'asset':images[name],'sourceSha256':hashlib.sha256(original).hexdigest(),'assetSha256':hashlib.sha256(data).hexdigest()});name=images[name]
  elif p.suffix in {'.js','.css','.html'}:
   text=data.decode('utf-8')
   for old,new in images.items():text=text.replace(old,new)
   data=text.encode('utf-8')
  archive.writestr(name,data)
 archive.writestr('hosting-assets.json',json.dumps(manifest,ensure_ascii=False))
with zipfile.ZipFile(output) as archive:
 assert {'panel.html','mobile.html','config.html'}<=set(archive.namelist())
 for name in archive.namelist():
  if name.endswith('.webp'):
   with Image.open(io.BytesIO(archive.read(name))) as image:image.verify()
print(f'Validated hosting ZIP: {output.name}, {output.stat().st_size} bytes; SHA256 {hashlib.sha256(output.read_bytes()).hexdigest()}')
