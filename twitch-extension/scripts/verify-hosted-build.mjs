import {readFileSync,readdirSync,existsSync} from 'node:fs';
import {join} from 'node:path';
const root='dist-hosted-test';
for(const file of ['panel.html','config.html'])if(!existsSync(join(root,file)))throw Error(`Missing Hosted Test entry: ${file}`);
const scripts=readdirSync(join(root,'assets')).filter(f=>f.endsWith('.js')).map(f=>readFileSync(join(root,'assets',f),'utf8')).join('\n');
const expected='https://rc1---funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app';
const standard='https://funjii-otter-rpg-backend-test-cwzl75ajla-ey.a.run.app';
if(!scripts.includes(expected)||scripts.includes(standard))throw Error('Hosted Test must use the rc1 backend; package rejected.');
console.log('Hosted Test backend and entry paths verified.');
