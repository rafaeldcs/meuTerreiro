import {spawnSync} from 'node:child_process';
import {readdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
const files=readdirSync(new URL('../app/tests/',import.meta.url)).filter(x=>x.endsWith('.test.mjs')).map(x=>'app/tests/'+x);
const steps=[['node',['--test',...files]],['dotnet',['test','api/Tests/Tests.csproj','--configuration','Release']],['dotnet',['build','api/WebAPI/WebAPI.csproj','--configuration','Release']],['npm',['run','build','--prefix','app']]];
for(const [bin,args]of steps){console.log(`\n> ${bin} ${args.join(' ')}`);const result=spawnSync(process.platform==='win32'&&bin==='npm'?'npm.cmd':bin,args,{cwd:root,stdio:'inherit',shell:false});if(result.error){console.error(`Não foi possível executar ${bin}: ${result.error.message}`);process.exit(1);}if(result.status!==0)process.exit(result.status||1);}
console.log('Builds e testes locais concluídos. MongoDB/HTTP, processador, restauração e celulares são verificações separadas.');
