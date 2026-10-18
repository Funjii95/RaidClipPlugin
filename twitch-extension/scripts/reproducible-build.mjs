import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {createRequire} from 'node:module';import {fileURLToPath} from 'node:url';import {execFileSync} from 'node:child_process';
const require=createRequire(import.meta.url);const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const run=(p,args=[])=>execFileSync(process.execPath,[p,...args],{cwd:root,stdio:'inherit'});
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const files=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);
const snapshot=()=>Object.fromEntries(files(path.join(root,'dist-desktop')).sort().map(p=>[path.relative(path.join(root,'dist-desktop'),p).replaceAll('\\','/'),{sha256:hash(fs.readFileSync(p)),bytes:fs.statSync(p).size}]));
run(require.resolve('typescript/bin/tsc'),['--noEmit']);const vite=path.join(path.dirname(require.resolve('vite/package.json')),'bin/vite.js');run(vite,['build','--mode','desktop-release','--outDir','dist-desktop']);const first=snapshot();run(vite,['build','--mode','desktop-release','--outDir','dist-desktop']);const second=snapshot();
if(JSON.stringify(first)!==JSON.stringify(second))throw Error('Release outputs differ between the two builds.');
fs.mkdirSync(path.join(root,'test-results'),{recursive:true});fs.writeFileSync(path.join(root,'test-results/reproducible-build.json'),JSON.stringify({node:process.version,lockfileSha256:hash(fs.readFileSync(path.join(root,'pnpm-lock.yaml'))),builds:2,identical:true,files:second},null,2));console.log('Two release builds are byte-identical.');
