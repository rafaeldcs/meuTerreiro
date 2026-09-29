// Further functional checks against the already seeded, isolated five-member center.
import {readFileSync,writeFileSync} from 'node:fs';
import {randomUUID} from 'node:crypto';
import assert from 'node:assert/strict';
const root=new URL('../',import.meta.url),origin='http://localhost:3180';
const users=JSON.parse(readFileSync(new URL('.secrets/simulation-access.json',root)));
const report={origin,checks:[],startedAt:new Date().toISOString()};
const output=new URL('artifacts/simulation/followup-report.json',root);
function save(){writeFileSync(output,JSON.stringify(report,null,2));}
function check(name,passed){report.checks.push({name,passed:!!passed});save();assert(passed,name);}
async function request(path,user,body,expected=200){
 const r=await fetch(origin+'/api'+path,{method:body===undefined?'GET':'POST',headers:{Origin:origin,'X-Terreiro-Client':'app','Content-Type':'application/json',...(user?.cookie?{Cookie:user.cookie}:{})},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(30000)});
 const raw=await r.text();let data;try{data=JSON.parse(raw)}catch{data=raw}
 report.checks.push({name:path,status:r.status,expected,passed:r.status===expected});save();assert.equal(r.status,expected,`${path}: ${data?.code}`);
 return{data,cookie:r.headers.getSetCookie().map(c=>c.split(';')[0]).join('; ')};
}
async function cmd(user,area,action,data,revision=1,expected=200){return(await request(`/${area}/${action}`,user,{operationId:randomUUID(),revision,data},expected)).data;}
async function rows(resource,user){return(await request('/records/'+resource+'?pageSize=100',user)).data.items;}
try{
 for(const user of Object.values(users)){user.cookie=(await request('/auth/login',null,{login:user.login,password:user.password})).cookie;}
 const {Member,Secretary,Coordinator,Treasury,Approver}=users;
 const docs=await rows('documents',Secretary),doc=docs.find(d=>d.title==='Documento exclusivamente sintético');assert(doc);
 // Positive control prevents a misspelled/nonexistent route from passing a denial test.
 await request('/evidence/'+doc.evidenceId+'/file',Secretary);
 await request('/evidence/'+doc.evidenceId+'/file',Member,undefined,404);
 check('Real private file route works for secretary and hides document from member',true);
 const period=new Intl.DateTimeFormat('en-CA',{timeZone:'America/Sao_Paulo',year:'numeric',month:'2-digit'}).format(new Date());
 await request('/reports/finance/'+period,Treasury);await request('/reports/finance/'+period,Member,undefined,403);
 const csv=(await request('/reports/ledger/'+period+'.csv',Treasury)).data;check('Real financial export contains synthetic movements',typeof csv==='string'&&csv.includes('SIM-'));
 await request('/reports/ledger/'+period+'.csv',Member,undefined,403);
 const fund=await cmd(Treasury,'finance','fund.create',{name:'Campanha fictícia',purpose:'Reposição exclusivamente sintética',goalCents:50000,restricted:true});check('Campaign starts without invented money',fund.balanceCents===0);
 const start=new Date(Date.now()+3*86400000),end=new Date(start.getTime()+3600000);
 let cleaning=(await request('/cleanings',Coordinator,{operationId:randomUUID(),title:'Troca e reagendamento fictícios',area:'Salão simulado',mode:'Team',target:2,startsAt:start.toISOString(),endsAt:end.toISOString(),memberIds:[Member.id],tasks:['Organizar mesa fictícia']})).data;
 const swap=await cmd(Member,'cleaning-actions','swap.request',{cleaningId:cleaning.id,substituteId:Secretary.id},cleaning.revision);
 await cmd(Coordinator,'cleaning-actions','swap.review',{swapId:swap.id,approve:true},swap.revision,409);
 await cmd(Member,'cleaning-actions','swap.accept',{swapId:swap.id,accept:true},swap.revision,403);
 const accepted=await cmd(Secretary,'cleaning-actions','swap.accept',{swapId:swap.id,accept:true},swap.revision);
 await cmd(Coordinator,'cleaning-actions','swap.review',{swapId:swap.id,approve:true},accepted.revision);
 cleaning=(await request('/cleanings/'+cleaning.id,Coordinator)).data;
 check('Approved swap replaces original without inventing presence',cleaning.assignments.length===1&&cleaning.assignments[0].userId===Secretary.id&&cleaning.assignments[0].participation==='Unverified');
 await cmd(Coordinator,'cleaning-actions','task.assign',{cleaningId:cleaning.id,taskId:cleaning.tasks[0].id,memberId:Secretary.id},cleaning.revision);
 cleaning=(await request('/cleanings/'+cleaning.id,Coordinator)).data;
 await cmd(Secretary,'cleaning-actions','task.report',{cleaningId:cleaning.id,taskId:cleaning.tasks[0].id},cleaning.revision,409);
 await cmd(Coordinator,'cleaning-actions','reschedule',{cleaningId:cleaning.id,startsAt:new Date(+start+86400000).toISOString(),endsAt:new Date(+end+86400000).toISOString()},cleaning.revision);
 cleaning=(await request('/cleanings/'+cleaning.id,Coordinator)).data;
 check('Rescheduling resets confirmation and increases publication version',cleaning.publicationVersion===2&&cleaning.assignments[0].response==='Pending');
 await request(`/cleanings/${cleaning.id}/response`,Secretary,{version:1,response:'Confirmed'},409);
 await cmd(Coordinator,'cleaning-actions','dispense',{cleaningId:cleaning.id,memberId:Secretary.id,reason:'Dispensa exclusivamente administrativa fictícia'},cleaning.revision);
 cleaning=(await request('/cleanings/'+cleaning.id,Coordinator)).data;check('Administrative dispensation is recorded separately',cleaning.assignments[0].dispensed===true);
 const series=await cmd(Coordinator,'cleaning-actions','series.create',{title:'Série semanal fictícia',area:'Salão',mode:'Team',target:5,nextStartsAt:new Date(+start+7*86400000).toISOString(),until:new Date(+start+28*86400000).toISOString(),durationMinutes:60,intervalWeeks:1,memberIds:[Member.id,Secretary.id],tasks:['Organizar espaço fictício']});
 await cmd(Coordinator,'cleaning-actions','series.stop',{seriesId:series.id},series.revision);
 check('Recurring schedule can be stopped',(await rows('series',Coordinator)).find(s=>s.id===series.id)?.active===false);
 const notices=(await request('/notifications',Member)).data;
 await request('/notifications/'+notices[0].id+'/open',Member,{},204);await request('/notifications/'+notices[0].id+'/archive',Member,{},204);
 await request('/notification-feed?filter=archived',Member);
 const receipt=(await rows('receipts',Treasury)).find(r=>r.memberId===Member.id);
 await request('/receipts/'+receipt.id,Member);await request('/receipts/'+receipt.id,Secretary,undefined,403);
 const dues=await rows('dues',Treasury);check('Follow-up preserves five dues and R$150 outstanding',dues.length===5&&dues.reduce((s,d)=>s+d.balanceCents,0)===15000);
 report.success=true;save();console.log(JSON.stringify({success:true,checks:report.checks.length}));
}catch(error){report.success=false;report.failure=error.message;save();console.error(report.failure);process.exitCode=1;}
