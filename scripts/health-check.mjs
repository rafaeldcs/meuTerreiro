import process from 'node:process';
const raw=process.argv[2];const origin=new URL(raw);
if(!['localhost','127.0.0.1'].includes(origin.hostname)||origin.protocol!=='http:')throw new Error('Este verificador de ensaio só aceita localhost HTTP.');
let ok=false;
for(let n=0;n<60;n++){
 try{const r=await fetch(new URL('/health',origin),{signal:AbortSignal.timeout(4000)});if(r.ok){ok=true;break;}}catch{}
 await new Promise(r=>setTimeout(r,2000));
}
if(!ok)throw new Error('API não ficou saudável; consulte os logs sem compartilhar segredos.');
const response=await fetch(new URL('/api/status',origin));if(!response.ok)throw new Error('Status da API indisponível.');
const status=await response.json();if(status.readyForProduction!==false)throw new Error('Ensaio deve continuar em homologação.');
console.log('API responde. Isso não verifica saldos, restauração dos anexos nem push em celulares.');
