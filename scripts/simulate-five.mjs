// Original synthetic scenario. Run only in the isolated Compose simulation.
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {randomBytes,randomUUID} from 'node:crypto';
const root=new URL('../',import.meta.url);
const env=Object.fromEntries(readFileSync(new URL('.env',root),'utf8').split('\n').filter(l=>l&&!l.startsWith('#')).map(l=>{const i=l.indexOf('=');return[l.slice(0,i),l.slice(i+1)];}));
const origin=env.APP_ORIGIN;
assert.equal(origin,'http://localhost:3180','Only the dedicated local simulation is authorized');
const output=new URL('artifacts/simulation/',root);mkdirSync(output,{recursive:true});
const plan=JSON.parse(readFileSync(new URL('artifacts/local-ai-plan.json',root),'utf8'));
for(const name of ['AUTHENTICATION','PERMISSIONS','DUES','TREASURY','STOCK','ADMINISTRATION','COMMUNICATION','DOCUMENTS','BROWSER','EVIDENCE'])assert.ok(plan.actions.includes('CHECK_'+name),'Local model omitted '+name);
const report={house:'Centro de simulação — cinco médiuns',origin,monthlyCents:10000,startedAt:new Date().toISOString(),proposer:plan,checks:[],stages:[],limitations:[]};
const actors={};let stage='authentication';
function persist(){writeFileSync(new URL('http-report.json',output),JSON.stringify(report,null,2));}
function check(name,condition){report.checks.push({stage,name,passed:!!condition});persist();assert.ok(condition,name);}
async function req(path,{actor,body,expected=200,method,headers={}}={}){
 const start=performance.now();let res;
 for(let attempt=0;attempt<2;attempt++){
  res=await fetch(origin+'/api'+path,{method:method||(body===undefined?'GET':'POST'),headers:{Origin:origin,'X-Terreiro-Client':'app',...(actor?{Cookie:actor.cookie}:{}),...(body instanceof FormData?{}:{'Content-Type':'application/json'}),...headers},body:body===undefined?undefined:body instanceof FormData?body:JSON.stringify(body),signal:AbortSignal.timeout(30000)});
  if(res.status!==429||expected===429||attempt===1)break;
  await res.arrayBuffer();await new Promise(r=>setTimeout(r,61000));
 }
 const raw=await res.text();let data;try{data=JSON.parse(raw)}catch{data=raw}
 report.checks.push({stage,name:(method||(body===undefined?'GET':'POST'))+' '+path,status:res.status,expected,passed:res.status===expected,ms:Math.round(performance.now()-start),...(res.status!==expected?{error:typeof data==='object'?data?.code:res.statusText}:{})});persist();
 assert.equal(res.status,expected,`${path}: expected ${expected}, received ${res.status}; ${typeof data==='object'?data?.code||data?.error:''}`);
 return {data,cookie:res.headers.getSetCookie().map(c=>c.split(';')[0]).join('; ')};
}
async function login(user){const r=await req('/auth/login',{body:{login:user.login,password:user.password}});user.cookie=r.cookie;Object.assign(user,r.data);return user;}
async function command(actor,area,action,data,revision=1,expected=200,operationId=randomUUID()){return(await req(`/${area}/${action}`,{actor,body:{operationId,revision,data},expected})).data;}
async function rows(resource,actor=actors.Admin){return(await req('/records/'+resource+'?pageSize=100',{actor})).data.items;}
async function create(name,key,role='Member'){
 const temporary=randomBytes(24).toString('base64url'),user={name,login:'sim.'+key,password:temporary};
 const made=await req('/members',{actor:actors.Admin,body:{...user,role}});user.id=made.data.id;
 await login(user);const password=randomBytes(24).toString('base64url');
 await req('/auth/password',{actor:user,body:{currentPassword:temporary,newPassword:password},expected:204});user.password=password;await login(user);return user;
}
async function grant(user,role,isMember){
 const change=await command(actors.Admin,'security','request',{userId:user.id,role,active:true,isMember,reason:'Perfil exclusivamente fictício para simulação administrativa'});
 await command(actors.Admin,'security','review',{changeId:change.id,approve:true},change.revision,403);
 await command(actors.Admin2,'security','review',{changeId:change.id,approve:true},change.revision);
 await req('/auth/me',{actor:user,expected:401});await login(user);
}
const date=new Intl.DateTimeFormat('en-CA',{timeZone:'America/Sao_Paulo',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date()),period=date.slice(0,7);
let bank,cash,dues,receipt,lot,item,event,members;
try{
 await req('/members',{expected:401});await req('/auth/login',{body:{login:'admin',password:'synthetic-invalid'},headers:{Origin:'https://untrusted.invalid'},expected:403});
 actors.Admin={login:env.BOOTSTRAP_LOGIN,password:env.BOOTSTRAP_PASSWORD};await login(actors.Admin);
 check('Fresh disposable bootstrap requires a password change',actors.Admin.mustChangePassword===true);
 const password=randomBytes(24).toString('base64url');await req('/auth/password',{actor:actors.Admin,body:{currentPassword:actors.Admin.password,newPassword:password},expected:204});actors.Admin.password=password;await login(actors.Admin);
 actors.Admin2=await create('Revisor técnico fictício','admin2','Admin');
 for(const [key,name] of [['Member','Médium Ana — simulação'],['Treasury','Médium Bruno — tesouraria'],['Stock','Médium Carla — estoque'],['Secretary','Médium Diego — secretaria'],['Coordinator','Médium Elisa — coordenação']])actors[key]=await create(name,key.toLowerCase());
 actors.Reviewer=await create('Auditor fictício — sem mensalidade','reviewer');
 members=['Member','Treasury','Stock','Secretary','Coordinator'].map(k=>actors[k]);
 stage='permissions';
 for(const key of ['Treasury','Stock','Secretary','Coordinator','Reviewer'])await grant(actors[key],key,key!=='Reviewer');
 // Bootstrap is an operator outside the five billed members.
 await grant(actors.Admin,'Admin',false);
 // Granting a normal Admin removes bootstrap financial privileges; they must remain absent.
 check('Regular administrator has no implicit treasury permission',!actors.Admin.permissions.includes('finance.write'));
 // All financial test writes now use Treasury; independent decisions use a second Treasury operator.
 const secondChange=await command(actors.Admin,'security','request',{userId:actors.Admin2.id,role:'Admin',active:true,isMember:false,reason:'Revisor técnico fora do quadro de médiuns'});
 // The beneficiary cannot approve their own change. The request is intentionally rejected.
 await command(actors.Admin2,'security','review',{changeId:secondChange.id,approve:true},secondChange.revision,403);
 const mapped={Admin:['members.write','security.manage','office.read','office.write','communications.publish'],Admin2:['members.write','security.manage','office.read','office.write','communications.publish'],Member:[],Treasury:['finance.read','finance.write','finance.approve'],Stock:['stock.read','stock.write','stock.approve'],Secretary:['office.read','office.write','communications.publish'],Coordinator:[],Reviewer:['finance.read','stock.read','office.read','audit.read']};
 const resources={accounts:'finance.read',ledger:'finance.read',rules:'finance.read',receipts:'finance.read',closings:'finance.read',lots:'stock.read',documents:'office.read','access-changes':'security.manage'};
 for(const [role,user]of Object.entries(actors)){
  check(role+' permission set is explicit',JSON.stringify([...user.permissions].sort())===JSON.stringify([...mapped[role]].sort()));
  for(const [resource,permission]of Object.entries(resources))await req('/records/'+resource,{actor:user,expected:mapped[role].includes(permission)?200:403});
  await command(user,'finance','account.create',{name:'Forbidden account',kind:'Bank'},1,role==='Treasury'?200:403);
 }
 // Independent treasury reviewer: use a staff account, not a sixth médium.
 actors.Approver=await create('Aprovador financeiro fictício','approver');await grant(actors.Approver,'Treasury',false);
 // Mark the second technical admin non-member through an independent requester.
 const change=await command(actors.Admin2,'security','request',{userId:actors.Admin2.id,role:'Admin',active:true,isMember:false,reason:'Operador técnico fora do quadro de médiuns'});
 await command(actors.Admin,'security','review',{changeId:change.id,approve:true},change.revision);await login(actors.Admin2);
 const allMembers=(await req('/members',{actor:actors.Admin})).data;
 check('Exactly five active members in the simulation',allMembers.filter(u=>u.isMember).length===5);
 writeFileSync(new URL('.secrets/simulation-access.json',root),JSON.stringify(Object.fromEntries(Object.entries(actors).map(([k,v])=>[k,{login:v.login,password:v.password,id:v.id,name:v.name}]))),{mode:0o600});
 stage='dues';
 const treasury=actors.Treasury,approver=actors.Approver;
 bank=await command(treasury,'finance','account.create',{name:'Banco simulado — nenhum dinheiro real',kind:'Bank'});
 cash=await command(treasury,'finance','account.create',{name:'Caixa físico simulado',kind:'Cash'});
 await command(treasury,'finance','rule.create',{name:'Mensalidade simulada — cinco médiuns',amountCents:10000,dueDay:1,effectiveFrom:period,allActiveMembers:false,memberIds:members.map(m=>m.id)});
 check('Five monthly dues generated',(await command(treasury,'finance','dues.generate',{period})).generated===5);
 check('Repeated generation produces no duplicates',(await command(treasury,'finance','dues.generate',{period})).generated===0);
 dues=await rows('dues',treasury);check('Nominal dues total R$500',dues.reduce((s,d)=>s+d.amountCents,0)===50000);
 const byUser=u=>dues.find(d=>d.memberId===u.id);
 await req('/dues/'+byUser(actors.Stock).id,{actor:actors.Member,expected:403});await req('/dues?month=13',{actor:actors.Member,expected:400});
 const exemption=await command(treasury,'finance','member.exemption.request',{memberId:actors.Secretary.id,reason:'Isenção exclusivamente fictícia aprovada para o cenário',effectiveFrom:period});
 const exemptionApproval=(await rows('approvals',treasury)).find(a=>a.id===exemption.approvalId);
 await command(treasury,'finance','approval.review',{approvalId:exemptionApproval.id,approve:true,note:'Não pode aprovar a própria solicitação'},exemptionApproval.revision,403);
 await command(approver,'finance','approval.review',{approvalId:exemptionApproval.id,approve:true,note:'Revisão independente da isenção fictícia'},exemptionApproval.revision);
 check('Exemption does not create a cash movement',(await rows('ledger',treasury)).length===0);
 async function receive(member,amount,reference){return await command(treasury,'finance','receipt.confirm',{accountId:bank.id,memberId:member?.id,occurredOn:date,amountCents:amount,financialReference:reference,confirmedCredit:true,payerName:'Pessoa fictícia',verificationNote:'Conferência simulada; nenhum banco ou dinheiro real'});}
 for(const [user,amount,total]of [[actors.Member,10000,10000],[treasury,5000,5000],[actors.Coordinator,10000,12000]]){
  const r=await receive(user,total,'SIM-'+user.login);const allocated=await command(treasury,'finance','receipt.allocate',{receiptId:r.id,allocations:[{dueId:byUser(user).id,amountCents:amount}]},r.revision);
  if(user===actors.Coordinator)receipt=allocated;
 }
 dues=await rows('dues',treasury);
 check('Paid R$250, exempt R$100, outstanding R$150',dues.reduce((s,d)=>s+d.paidCents,0)===25000&&dues.reduce((s,d)=>s+d.adjustmentCents,0)===-10000&&dues.reduce((s,d)=>s+d.balanceCents,0)===15000);
 check('Five distinct monthly situations',byUser(actors.Member).state==='Paid'&&byUser(treasury).state==='Partial'&&byUser(actors.Stock).state==='Open'&&byUser(actors.Secretary).state==='Exempt'&&byUser(actors.Coordinator).state==='Paid');
 check('Third-party surplus remains available',receipt.availableCents===2000);
 await command(treasury,'finance','receipt.allocate',{receiptId:receipt.id,allocations:[{dueId:byUser(actors.Stock).id,amountCents:2001}]},receipt.revision,409);
 stage='treasury';
 const receiptInput={accountId:bank.id,occurredOn:date,amountCents:1000,financialReference:'SIM-CONCURRENT',confirmedCredit:true,payerName:'Concorrência fictícia',verificationNote:'Teste sintético de idempotência concorrente'};
 const operation=randomUUID();const concurrent=await Promise.all([command(treasury,'finance','receipt.confirm',receiptInput,1,200,operation),command(treasury,'finance','receipt.confirm',receiptInput,1,200,operation)]);
 check('Concurrent same operation creates one receipt',concurrent[0].id===concurrent[1].id);
 await command(treasury,'finance','receipt.confirm',{...receiptInput,amountCents:1001},1,409,operation);
 await command(treasury,'finance','receipt.confirm',receiptInput,1,409);
 const expense=await command(treasury,'finance','expense.create',{title:'Material de limpeza fictício',kind:'Expense',category:'Limpeza',amountCents:4000,dueDate:date,beneficiary:'Fornecedor simulado'});
 await command(treasury,'finance','expense.pay',{expenseId:expense.id,accountId:bank.id,amountCents:4000,occurredOn:date,financialReference:'SIM-OUT',confirmedExecution:true},expense.revision,409);
 const approval=(await rows('approvals',treasury)).find(a=>a.id===expense.approvalId);
 await command(approver,'finance','approval.review',{approvalId:approval.id,approve:true,note:'Despesa fictícia aprovada independentemente'},approval.revision);
 const approved=(await rows('expenses',treasury)).find(e=>e.id===expense.id);
 await command(treasury,'finance','expense.pay',{expenseId:expense.id,accountId:bank.id,amountCents:4000,occurredOn:date,financialReference:'SIM-OUT',confirmedExecution:true},approved.revision);
 const cashSession=await command(treasury,'finance','cash.open',{accountId:cash.id,countedCents:0});
 await command(treasury,'finance','transfer',{fromId:bank.id,toId:cash.id,toCashSessionId:cashSession.id,amountCents:5000,occurredOn:date,financialReference:'SIM-TRANSFER'});
 const closedCash=await command(treasury,'finance','cash.close',{sessionId:cashSession.id,countedCents:5000,note:'Contagem fictícia confere com o saldo'},cashSession.revision);
 await command(approver,'finance','cash.review',{sessionId:closedCash.id},closedCash.revision);
 check('Transfers preserve the total balance R$240',(await rows('accounts',treasury)).reduce((s,a)=>s+a.balanceCents,0)===24000);
 stage='stock';
 event=await command(actors.Secretary,'operations','event.create',{title:'Encontro administrativo fictício',description:'Somente teste de gestão; nenhuma atividade religiosa real',location:'Salão de simulação',startsAt:new Date(Date.now()+86400000).toISOString(),endsAt:new Date(Date.now()+90000000).toISOString(),participantIds:members.map(m=>m.id),budgetCents:5000});
 item=await command(actors.Stock,'operations','stock.item.create',{name:'Detergente de simulação',unit:'un',category:'Limpeza',minimumMilli:2000,reusable:false});
 lot=await command(actors.Stock,'operations','stock.receive',{itemId:item.id,quantityMilli:10000,lot:'SIM-01',location:'Depósito fictício',reason:'Dez unidades para teste sem movimento financeiro'});
 await command(actors.Stock,'operations','stock.reserve',{lotId:lot.id,quantityMilli:11000,activityKind:'Event',activityId:event.id},lot.revision,409);
 const reservation=await command(actors.Stock,'operations','stock.reserve',{lotId:lot.id,quantityMilli:4000,activityKind:'Event',activityId:event.id},lot.revision);
 const issued=await command(actors.Stock,'operations','stock.issue',{reservationId:reservation.id,quantityMilli:4000},reservation.revision);
 await command(actors.Stock,'operations','stock.settle',{reservationId:issued.id,consumedMilli:5000,returnedMilli:0,lostMilli:0,reason:'Quantidade inválida'},issued.revision,409);
 await command(actors.Stock,'operations','stock.settle',{reservationId:issued.id,consumedMilli:2000,returnedMilli:1000,lostMilli:1000,reason:'Duas consumidas, uma devolvida e uma perda fictícia'},issued.revision);
 check('Stock settles consumption, return and loss exactly',(await rows('lots',actors.Stock)).find(l=>l.id===lot.id).onHandMilli===7000);
 const expired=await command(actors.Stock,'operations','stock.receive',{itemId:item.id,quantityMilli:1000,lot:'SIM-VENCIDO',location:'Quarentena',expiresOn:'2020-01-01',reason:'Lote vencido reservado somente para validação'});
 await command(actors.Stock,'operations','stock.reserve',{lotId:expired.id,quantityMilli:1000,activityKind:'Event',activityId:event.id},expired.revision,409);
 stage='administration';
 const supplier=await command(treasury,'operations','supplier.create',{name:'Fornecedor fictício da simulação',document:'SIMULACAO',contact:'Contato interno fictício',paymentDetails:'Sem dados bancários reais'});
 let purchase=await command(actors.Stock,'operations','purchase.create',{title:'Compra fictícia de reposição',supplierId:supplier.id,dueDate:date,freightCents:0,lines:[{itemId:item.id,quantityMilli:4000,totalCents:2000}]});
 const pa=await command(actors.Stock,'operations','purchase.submit',{purchaseId:purchase.id},purchase.revision);
 await command(treasury,'operations','purchase.review',{approvalId:pa.id,approve:true,note:'Compra sintética revisada'},pa.revision);
 purchase=(await rows('purchases',actors.Stock)).find(p=>p.id===purchase.id);
 for(const qty of [1000,3000]){await command(actors.Stock,'operations','purchase.receive',{purchaseId:purchase.id,lineId:purchase.lines[0].id,quantityMilli:qty,lot:'SIM-COMPRA',location:'Depósito fictício',reason:'Recebimento parcial sintético'},purchase.revision);purchase=(await rows('purchases',actors.Stock)).find(p=>p.id===purchase.id);}
 check('Purchase fully received after partial deliveries',purchase.lines[0].receivedMilli===4000);
 const donation=await command(actors.Member,'operations','donation.promise',{kind:'Material',description:'Doação fictícia de duas unidades',itemId:item.id,quantityMilli:2000,estimatedCents:1000});
 await command(actors.Stock,'operations','donation.receive',{donationId:donation.id,quantityMilli:2000,lot:'SIM-DOACAO',location:'Depósito fictício'},donation.revision);
 const asset=await command(actors.Secretary,'operations','asset.create',{name:'Mesa fictícia',code:'SIM-MESA',location:'Salão',ownership:'House',estimatedCents:10000});
 const loan=await command(actors.Secretary,'operations','asset.move',{assetId:asset.id,kind:'Loan',memberId:actors.Member.id,returnDue:date,reason:'Empréstimo fictício'} ,asset.revision);
 await command(actors.Secretary,'operations','asset.move',{assetId:asset.id,kind:'Return',condition:'Good',location:'Salão fictício',reason:'Devolução fictícia conferida'},loan.revision);
 const maintenance=await command(actors.Secretary,'operations','maintenance.create',{assetId:asset.id,title:'Inspeção fictícia da mesa',dueDate:date,responsibleId:actors.Member.id});
 await command(actors.Secretary,'operations','maintenance.complete',{maintenanceId:maintenance.id,note:'Inspeção sintética concluída'},maintenance.revision);
 const task=await command(actors.Secretary,'operations','task.create',{title:'Organizar documentos fictícios',description:'Atividade de validação administrativa',dueDate:date,responsibleId:actors.Member.id});
 await command(actors.Stock,'operations','task.complete',{taskId:task.id},task.revision,403);
 await command(actors.Member,'operations','task.complete',{taskId:task.id},task.revision);
 const budget=await command(treasury,'operations','budget.create',{period,category:'Limpeza',plannedIncomeCents:40000,plannedExpenseCents:10000});
 await command(approver,'operations','budget.approve',{budgetId:budget.id},budget.revision);
 stage='communication';
 const cleaning=(await req('/cleanings',{actor:actors.Coordinator,body:{operationId:randomUUID(),title:'Limpeza fictícia com cinco médiuns',area:'Salão',mode:'Team',target:5,startsAt:new Date(Date.now()+86400000).toISOString(),endsAt:new Date(Date.now()+90000000).toISOString(),memberIds:members.map(m=>m.id),tasks:['Organizar espaço de simulação']}})).data;
 await req(`/cleanings/${cleaning.id}/response`,{actor:actors.Member,body:{version:1,response:'Confirmed'}});
 const after=(await req('/cleanings/'+cleaning.id,{actor:actors.Coordinator})).data;check('Confirming does not mark presence',after.assignments.every(a=>a.participation==='Unverified'));
 const announcement=await command(actors.Secretary,'operations','announcement.publish',{title:'Comunicado da simulação',body:'Aviso fictício para validar leitura e ciência separadas.',recipientIds:members.map(m=>m.id),requireAcknowledgement:true});
 const notices=(await req('/notifications',{actor:actors.Member})).data;
 await req('/notifications/'+notices[0].id+'/read',{actor:actors.Member,body:{},expected:204});
 await command(actors.Member,'operations','announcement.ack',{announcementId:announcement.id,version:1},announcement.revision);
 await req('/preferences',{actor:actors.Member,body:{operationId:randomUUID(),revision:1,data:{pushEnabled:false,quietStartHour:22,quietEndHour:7,mutedCategories:['Announcement']}}});
 await req('/preferences',{actor:actors.Member,body:{operationId:randomUUID(),revision:1,data:{quietStartHour:25,quietEndHour:7}},expected:400});
 check('Real bank integration remains disabled',(await req('/pix/config',{actor:actors.Member,expected:200})).data.enabled===false);
 stage='documents';
 const form=new FormData();form.set('operationId',randomUUID());form.set('purpose','Receipt');form.set('memberId',actors.Stock.id);form.set('dueIds',byUser(actors.Stock).id);form.set('declaredCents','10000');form.set('declaredDate',date);
 form.set('file',new Blob([readFileSync(new URL('app/public/icons/icon-192.png',root))],{type:'image/png'}),'comprovante-sintetico.png');
 const beforeDue=byUser(actors.Stock).balanceCents;const evidence=(await req('/evidence/upload',{actor:actors.Stock,body:form})).data;
 check('Uploading a receipt does not pay a due',(await rows('dues',treasury)).find(d=>d.memberId===actors.Stock.id).balanceCents===beforeDue);
 await req('/evidence/'+evidence.id+'/analysis',{actor:actors.Member,expected:404});
 async function awaitScan(id,actor){
  let analysis;for(let attempt=0;attempt<30;attempt++){
   analysis=(await req('/evidence/'+id+'/analysis',{actor})).data;
   if(analysis.scanState!=='Quarantine')break;
   await new Promise(r=>setTimeout(r,2000));
  }
  check('Real scanner releases the synthetic image',analysis.scanState==='Clean');
  check('Analysis explicitly is not proof of payment',analysis.notProofOfPayment===true);
 }
 await awaitScan(evidence.id,actors.Stock);
 const documentForm=new FormData();documentForm.set('operationId',randomUUID());documentForm.set('purpose','Document');documentForm.set('memberId',actors.Secretary.id);
 documentForm.set('file',new Blob([readFileSync(new URL('app/public/icons/icon-192.png',root))],{type:'image/png'}),'documento-sintetico.png');
 const documentEvidence=(await req('/evidence/upload',{actor:actors.Secretary,body:documentForm})).data;
 await awaitScan(documentEvidence.id,actors.Secretary);
 const document=await command(actors.Secretary,'operations','document.create',{evidenceId:documentEvidence.id,title:'Documento exclusivamente sintético',category:'Simulação',responsibleId:actors.Secretary.id,reviewDate:date});
 check('Document registered with a released private attachment',document.evidenceId===documentEvidence.id);
 await req('/evidence/'+documentEvidence.id+'/file',{actor:actors.Secretary});
 await req('/evidence/'+documentEvidence.id+'/file',{actor:actors.Member,expected:404});
 stage='treasury';
 const closing=await command(treasury,'finance','closing.prepare',{period,note:'Fechamento do centro fictício; nenhuma operação bancária real'});
 await command(treasury,'finance','closing.review',{closingId:closing.id},closing.revision,403);
 await command(approver,'finance','closing.review',{closingId:closing.id},closing.revision);
 await command(treasury,'finance','receipt.confirm',{...receiptInput,financialReference:'SIM-CLOSED'},1,409);
 stage='evidence';
 const balances=await rows('accounts',treasury);const ledger=await rows('ledger',treasury);
 check('Ledger and account balances reconcile',ledger.reduce((s,e)=>s+e.signedCents,0)===balances.reduce((s,a)=>s+a.balanceCents,0));
 report.summary={mediumCount:5,monthlyCents:10000,period,dues:dues.map(d=>({state:d.state,amountCents:d.amountCents,paidCents:d.paidCents,balanceCents:d.balanceCents})),accountTotalCents:balances.reduce((s,a)=>s+a.balanceCents,0)};
 report.success=true;report.limitations.push('Browser checks are a separate report','No real bank, physical mobile push or production operation','Backlog capabilities not implemented are not qualified');persist();
 console.log(JSON.stringify({success:true,checks:report.checks.length,summary:report.summary}));
}catch(error){report.success=false;report.failure={stage,message:error.message};persist();console.error(JSON.stringify(report.failure));process.exitCode=1;}
