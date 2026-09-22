#!/usr/bin/env bash
# Run on the selected host, after CI/review. Does not configure DNS or TLS.
# Does not touch ShopAir, remove volumes, run destructive smoke, or upload secrets.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
command -v node >/dev/null || { echo "Node.js 22 é necessário." >&2; exit 1; }
command -v docker >/dev/null || { echo "Docker/Compose é necessário." >&2; exit 1; }
docker compose version >/dev/null
node --input-type=module <<'NODE'
import {readFileSync} from 'node:fs';
const values=Object.fromEntries(readFileSync('.env','utf8').split('\n').filter(x=>x&&!x.startsWith('#')).map(x=>{const i=x.indexOf('=');return[x.slice(0,i),x.slice(i+1)];}));
if(values.APP_ENVIRONMENT!=='Staging') throw Error('Este script exige Staging. Use o Compose local para Development.');
if(!/^https:\/\/[a-z0-9.-]+$/i.test(values.APP_ORIGIN||'')) throw Error('Origem HTTPS inválida.');
if(values.BIND_ADDRESS!=='127.0.0.1') throw Error('O gateway deve ficar restrito ao loopback, atrás do proxy HTTPS.');
if(!Number.isInteger(Number(values.HTTP_PORT))||Number(values.HTTP_PORT)<1024||Number(values.HTTP_PORT)>65535) throw Error('Porta local inválida.');
for(const key of ['MONGO_ROOT_PASSWORD','MONGO_APP_PASSWORD','BOOTSTRAP_PASSWORD','PROCESSOR_KEY'])if((values[key]||'').length<14)throw Error(`Configure ${key} localmente.`);
console.log('Configuração básica validada. Segredos não foram exibidos.');
NODE
node --test app/tests/*.test.mjs
docker compose config --quiet
if [[ -r /proc/meminfo ]]; then
  available=$(awk '/MemAvailable/ {print $2}' /proc/meminfo)
  if [[ "$available" -lt 6291456 && "${ACK_LIMITED_MEMORY:-}" != "yes" ]]; then
    echo "Há menos de 6 GiB livres. ClamAV pode usar 3–4 GiB, além de Mongo/API/app. Não iniciar na VPS da ShopAir sem revisar a capacidade. ACK_LIMITED_MEMORY=yes é uma exceção explícita, não recomendação." >&2
    exit 1
  fi
fi
docker compose build
docker compose up -d
node --input-type=module <<'NODE'
import {readFileSync} from 'node:fs';
const match=readFileSync('.env','utf8').match(/^HTTP_PORT=(\d+)$/m);
const local=`http://127.0.0.1:${match[1]}`;
let healthy=false;
for(let i=0;i<60;i++){
 try{
  const health=await fetch(local+'/health',{signal:AbortSignal.timeout(3000)});
  const status=await fetch(local+'/api/status',{signal:AbortSignal.timeout(3000)});
  if(health.ok&&status.ok){const data=await status.json();if(data.version==='0.2.1-alpha.1'&&data.readyForProduction===false){healthy=true;break;}}
 }catch{}
 await new Promise(resolve=>setTimeout(resolve,2000));
}
if(!healthy)throw Error('Saúde local não confirmada. Inspecione os serviços; nenhum volume foi apagado.');
console.log('API respondeu no gateway local. Verifique agora o domínio HTTPS e a instalação no celular.');
console.log('Esta verificação não comprova a publicação externa, transações de negócio ou entrega de push.');
NODE
