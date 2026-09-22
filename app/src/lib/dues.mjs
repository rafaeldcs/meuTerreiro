import {today} from './administration.mjs';

export const MONTHS=Object.freeze(['Janeiro','Fevereiro','Março','Abril','Maio','Junho','Julho','Agosto','Setembro','Outubro','Novembro','Dezembro']);
export const DUE_STATES=Object.freeze([
 ['all','Todas as situações'],['paid','Pagas (inclui isentas)'],['exempt','Somente isentas'],
 ['pending','Com saldo em aberto'],['partial','Pagamento parcial'],['cancelled','Canceladas']
]);
export function currentDueFilters(now=new Date()){
 const period=today(now).slice(0,7);
 return {year:period.slice(0,4),month:String(Number(period.slice(5,7))),state:'all',q:''};
}
export function dueQuery(filters,page=1){
 const p=new URLSearchParams({page:String(page),pageSize:'20'});
 if(filters.year)p.set('year',String(filters.year));
 if(filters.month)p.set('month',String(filters.month));
 if(filters.state&&filters.state!=='all')p.set('state',filters.state);
 if(filters.q?.trim())p.set('q',filters.q.trim());
 return '/dues?'+p.toString();
}
export function competenceLabel(value){
 const match=/^(\d{4})-(0[1-9]|1[0-2])$/.exec(value||'');
 return match?`${MONTHS[Number(match[2])-1]} de ${match[1]}`:'Competência não informada';
}
export function duePresentation(due){
 if(due.cancelled||due.state==='Cancelled')return {label:'Cancelado',observation:'',tone:'cancelled'};
 if(due.exempt||due.state==='Exempt'){
  if(due.balanceCents!==0||due.paidCents!==0)return {label:'Isenção a revisar',observation:'Consulte a tesouraria',tone:'pending'};
  return {label:'Pago',observation:'Isento',tone:'exempt'};
 }
 if(due.balanceCents===0)return {label:'Pago',observation:'',tone:'paid'};
 if(due.paidCents>0)return {label:'Parcial',observation:'Pagamento parcial',tone:'partial'};
 return {label:'Em aberto',observation:'',tone:'open'};
}
export function canRequestExemption(due){
 return !due.cancelled&&!due.exempt&&due.state!=='Cancelled'&&due.state!=='Exempt'&&due.paidCents===0&&due.balanceCents>0;
}
export function filterPeriodLabel(filters){
 if(filters.year&&filters.month)return competenceLabel(`${filters.year}-${String(filters.month).padStart(2,'0')}`);
 if(filters.year)return `Todos os meses de ${filters.year}`;
 if(filters.month)return `${MONTHS[Number(filters.month)-1]} de todos os anos`;
 return 'Todo o histórico';
}
