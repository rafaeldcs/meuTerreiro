import {today} from './administration.mjs';
export const currentExemptionPeriod=(now=new Date())=>today(now).slice(0,7);
export function nextExemptionMonth(month){
 if(!/^(20[0-9]{2}|2100)-(0[1-9]|1[0-2])$/.test(month))throw new Error('Competência inválida.');
 const [y,m]=month.split('-').map(Number);return `${m===12?y+1:y}-${String(m===12?1:m+1).padStart(2,'0')}`;
}
export function exemptionState(rule,month=currentExemptionPeriod()){
 if(rule.state==='Pending')return {label:'Aguardando aprovação',tone:'pending'};
 if(rule.state==='Rejected')return {label:'Não aprovada',tone:'muted'};
 if(rule.state==='Withdrawn')return {label:'Solicitação retirada',tone:'muted'};
 if(rule.state!=='Approved')return {label:'Requer conferência',tone:'muted'};
 // End-before-start is permitted only to cancel a future benefit at its starting month.
 if((rule.endedFrom&&rule.endedFrom<=rule.effectiveFrom)||
    (rule.endedFrom&&month>=rule.endedFrom)||(rule.effectiveTo&&month>rule.effectiveTo))return {label:'Encerrada',tone:'muted'};
 if(month<rule.effectiveFrom)return {label:'Isenção programada',tone:'pending'};
 return {label:'Isento de mensalidade',tone:'exempt'};
}
export function canEndExemption(rule,month=currentExemptionPeriod()){
 if(rule.state!=='Approved'||rule.endedFrom||rule.pendingEnd)return false;
 const from=[rule.effectiveFrom,nextExemptionMonth(month)].sort().at(-1);
 return !rule.effectiveTo||from<=rule.effectiveTo;
}
export function nextGrantPeriod(rules,period=currentExemptionPeriod()){
 let next=period;
 for(const rule of rules){
  if(!['Pending','Approved'].includes(rule.state))continue;
  const boundaries=[rule.effectiveTo?nextExemptionMonth(rule.effectiveTo):null,rule.endedFrom].filter(Boolean).sort();
  const end=boundaries[0];
  if(end&&end<=rule.effectiveFrom)continue;
  if(!end)return null;
  if(end>next)next=end;
 }
 return next;
}
export const EXEMPTION_EXCEPTION_LABELS={HasPayment:'Pagamento existente preservado; eventual crédito exige revisão.',PeriodClosed:'Período fechado preservado; solicite reabertura ou ajuste individual.',Cancelled:'Mensalidade cancelada preservada.',AlreadyExempt:'Isenção anterior preservada.',AlreadySettled:'Mensalidade já regularizada preservada.'};
export function grantExemptionAction(member,period=currentExemptionPeriod()){
 return {label:'Isentar este médium',permission:'finance.write',endpoint:'/finance/member.exemption.request',context:member.name||member.memberName,
  fields:[{key:'reason',label:'Motivo da isenção',type:'textarea',required:true,minLength:5,maxLength:500,help:'Fica no cadastro e é copiado para os detalhes das mensalidades abrangidas. Não informe dados íntimos.'},
   {key:'effectiveFrom',label:'A partir de',type:'month',required:true,default:period,min:period},
   {key:'effectiveTo',label:'Até (opcional)',type:'month',required:false,default:'',min:period,help:'Vazio: sem prazo, até encerramento aprovado.'}],
  prepare:values=>({...values,memberId:member.id||member.memberId}),submitLabel:'Solicitar isenção do médium',
  notice:'Uma única aprovação aplica a isenção às competências na vigência. Mensalidades já geradas e sem pagamento são ajustadas em períodos abertos. Pagamentos existentes e períodos fechados são preservados e listados para conferência.'};
}
export function endExemptionAction(rule,period=currentExemptionPeriod()){
 const from=[rule.effectiveFrom,nextExemptionMonth(period)].sort().at(-1);
 return {label:'Encerrar isenção do médium',permission:'finance.write',endpoint:'/finance/member.exemption.end',idKey:'exemptionId',
  fields:[{key:'endedFrom',label:'Voltar a cobrar a partir de',type:'month',required:true,default:from,min:from,max:rule.effectiveTo||undefined},
   {key:'reason',label:'Motivo do encerramento',type:'textarea',required:true,minLength:5,maxLength:500}],submitLabel:'Solicitar encerramento',
  notice:'Requer revisão independente. Preserva o mês corrente e os meses anteriores. Mensalidades futuras geradas por esta regra voltam ao valor devido a partir da competência escolhida, sem apagar outros descontos.'};
}
export function withdrawExemptionAction(){return {label:'Retirar solicitação pendente',permission:'finance.write',endpoint:'/finance/member.exemption.withdraw',idKey:'exemptionId',fields:[{key:'reason',label:'Motivo da retirada',type:'textarea',required:true,minLength:5,maxLength:500}],submitLabel:'Retirar solicitação',notice:'Somente o solicitante pode retirar seu pedido antes da aprovação. Não altera mensalidades.'};}
