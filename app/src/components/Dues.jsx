'use client';
import Link from 'next/link';
import {useCallback,useState} from 'react';
import {ChevronLeft,ChevronRight,RefreshCw,Search,Plus} from 'lucide-react';
import {has,money,safeDisplay} from '@/lib/administration.mjs';
import {MONTHS,DUE_STATES,currentDueFilters,dueQuery,competenceLabel,duePresentation,filterPeriodLabel} from '@/lib/dues.mjs';
import {MODULES} from '@/lib/modules.mjs';
import {useSession} from './Session';
import {Heading,Loading,ErrorNotice,Empty,useResource} from './UI';
import {ActionForm,Dialog} from './Administration';

export function DueStatus({due,onOpen}){
 const state=duePresentation(due);
 const content=<><span>{state.label}</span>{state.observation&&<small>{state.observation}</small>}</>;
 return onOpen?<button className={'due-status state-'+state.tone} type="button" onClick={onOpen} aria-label={`Abrir detalhes: ${state.label}${state.observation?' · '+state.observation:''}`}>{content}</button>:<span className={'due-status state-'+state.tone}>{content}</span>;
}
export function DuesFilters({value,years=[],onChange,onCurrent,onAll}){
 const yearOptions=[...new Set([...years,currentDueFilters().year,value.year].filter(Boolean))].sort().reverse();
 return <section className="dues-filters" aria-label="Filtros de mensalidades">
  <label htmlFor="dues-year">Ano<select id="dues-year" value={value.year} onChange={e=>onChange({...value,year:e.target.value})}><option value="">Todos os anos</option>{yearOptions.map(y=><option key={y} value={y}>{y}</option>)}</select></label>
  <label htmlFor="dues-month">Mês<select id="dues-month" value={value.month} onChange={e=>onChange({...value,month:e.target.value})}><option value="">Todos os meses</option>{MONTHS.map((name,i)=><option key={name} value={String(i+1)}>{name}</option>)}</select></label>
  <label htmlFor="dues-state">Situação<select id="dues-state" value={value.state} onChange={e=>onChange({...value,state:e.target.value})}>{DUE_STATES.map(([key,label])=><option key={key} value={key}>{label}</option>)}</select></label>
  <div className="dues-filter-actions"><button className="button secondary" type="button" onClick={onCurrent}>Mês atual</button><button className="button secondary" type="button" onClick={onAll}>Todo o histórico</button></div>
 </section>;
}
export function DuesTable({rows,user,onDetails,onAction}){
 const actions=MODULES.mensalidades.rowActions;
 return <div className="table-scroll"><table className="records-table dues-table"><thead><tr>{['Competência','Membro','Valor original','Recebido','Saldo','Situação','Ações'].map(t=><th key={t} scope="col">{t}</th>)}</tr></thead><tbody>{rows.map(due=><tr key={due.id}>
  <td data-label="Competência"><button type="button" className="due-open" onClick={()=>onDetails(due)}>{competenceLabel(due.competence)}</button><small className="field-help">Vence em {safeDisplay(due.dueDate,'date')}</small></td>
  <td data-label="Membro">{due.memberName}</td><td data-label="Valor original" className="numeric">{money(due.amountCents)}</td><td data-label="Recebido" className="numeric">{money(due.paidCents)}</td><td data-label="Saldo" className="numeric">{money(due.balanceCents)}</td>
  <td data-label="Situação"><DueStatus due={due} onOpen={()=>onDetails(due)}/></td>
  <td data-label="Ações"><div className="row-actions"><button className="button compact secondary" type="button" onClick={()=>onDetails(due)}>Ver detalhes</button>{actions.filter(a=>has(user,a.permission)&&(!a.when||a.when(due,user))).map(a=><button className="button compact secondary" type="button" key={a.label} onClick={()=>onAction(a,due)}>{a.label}</button>)}</div></td>
 </tr>)}</tbody></table></div>;
}
export function DueDetailsContent({data}){
 const {due,exemption,payments=[],adjustments=[]}=data;
 return <div className="due-detail-content"><div className="due-detail-title"><div><h3>{competenceLabel(due.competence)}</h3><p>{data.memberName}</p></div><DueStatus due={due}/></div>
  {due.exempt&&<section className="notice due-exemption" aria-label="Motivo da isenção"><strong>{exemption?.origin==='Member'?'Isento pelo cadastro do médium':'Isento nesta mensalidade'}</strong><p>{exemption?.reason||'O motivo não está disponível neste registro antigo. Consulte a tesouraria.'}</p><small>Isenção aprovada em {safeDisplay(exemption?.approvedAt,'datetime')}{exemption?.approvedByName?' · '+exemption.approvedByName:''}</small><p>Regularização sem recebimento financeiro. Não foi criado um pagamento no caixa.</p></section>}
  <dl className="detail-list"><div><dt>Valor original</dt><dd>{money(due.amountCents)}</dd></div><div><dt>Ajustes / isenção</dt><dd>{money(due.adjustmentCents)}</dd></div><div><dt>Valor recebido e destinado a esta mensalidade</dt><dd>{money(due.paidCents)}</dd></div><div><dt>Saldo a pagar</dt><dd>{money(due.balanceCents)}</dd></div><div><dt>Vencimento</dt><dd>{safeDisplay(due.dueDate,'date')}</dd></div><div><dt>Competência</dt><dd>{due.competence}</dd></div></dl>
  <h3>Pagamentos desta mensalidade</h3>{!payments.length?<p className="field-help">{due.exempt?'Nenhum pagamento: mensalidade regularizada por isenção.':'Nenhum recebimento vinculado a esta mensalidade.'}</p>:<div className="due-history">{payments.map(p=><article key={p.id} className="due-history-item"><strong>{money(p.netCents)} destinados a este mês</strong><p>Data do recebimento: {safeDisplay(p.occurredOn,'date')} · {safeDisplay(p.verification,'state')}</p>{p.reversedCents>0&&<p>Destinação original: {money(p.amountCents)} · revertido: {money(p.reversedCents)}</p>}{p.financialReference&&<p>Referência: {p.financialReference}</p>}{p.sourceAvailable?<Link className="button compact secondary" href={'/recibos/'+p.receiptId}>Abrir recibo</Link>:<p className="notice">Fonte financeira não localizada. Consulte a tesouraria.</p>}</article>)}</div>}
  {adjustments.length>0&&<><h3>Histórico de solicitações e ajustes</h3><div className="due-history">{adjustments.map(a=><article key={a.id} className="due-history-item"><strong>{safeDisplay(a.state,'state')}{a.state==='Pending'&&!a.current?' · versão alterada; requer revisão':''}</strong><p className="due-reason">{a.reason}</p><small>Solicitado em {safeDisplay(a.createdAt,'datetime')}{a.reviewedAt?' · revisado em '+safeDisplay(a.reviewedAt,'datetime'):''}</small></article>)}</div></>}
 </div>;
}
function DueDetails({id,onClose}){
 const r=useResource('/dues/'+encodeURIComponent(id));
 return <Dialog title="Detalhes da mensalidade" onClose={onClose} wide><div className="modal-body"><ErrorNotice>{r.error}</ErrorNotice>{r.loading?<Loading/>:r.data?<DueDetailsContent data={r.data}/>:<button className="button secondary" onClick={r.reload}>Tentar novamente</button>}</div></Dialog>;
}
export default function DuesScreen(){
 const {user}=useSession();const [filters,setFilters]=useState(currentDueFilters),[page,setPage]=useState(1),[search,setSearch]=useState(''),[modal,setModal]=useState(null),[message,setMessage]=useState('');
 const resource=useResource(dueQuery(filters,page));const module=MODULES.mensalidades;
 const close=useCallback(()=>setModal(null),[]);
 const change=next=>{setFilters(next);setPage(1);};
 const reset=next=>{setSearch('');change(next);};
 const actions=module.actions.filter(a=>has(user,a.permission));
 const total=resource.data?.total||0;const pages=Math.max(1,Math.ceil(total/20));
 const saved=async()=>{setMessage(modal?.action?.successMessage||'Operação registrada. Confira o resultado na competência selecionada.');await resource.reload();};
 return <><Heading title="Mensalidades" action={actions.length>0&&<div className="heading-actions">{actions.map(a=><button key={a.label} className="button" onClick={()=>setModal({action:a,row:{}})}><Plus size={17}/>{a.label}</button>)}</div>}>Mês corrente ao abrir. Toque na competência ou na situação para consultar pagamentos e o motivo da isenção.</Heading>
  <nav className="section-links" aria-label="Áreas relacionadas">{module.links.filter(x=>has(user,x[2])).map(([href,label])=><Link key={href} href={href}>{label}</Link>)}{has(user,'finance.approve')&&<Link href="/aprovacoes">Aprovações pendentes</Link>}</nav>
  <DuesFilters value={filters} years={resource.data?.years} onChange={change} onCurrent={()=>reset(currentDueFilters())} onAll={()=>reset({year:'',month:'',state:'all',q:''})}/>
  <ErrorNotice>{resource.error}</ErrorNotice>{message&&<div className="notice success" role="status">{message}</div>}
  <section className="data-panel"><header className="table-toolbar"><form className="dues-search" onSubmit={e=>{e.preventDefault();change({...filters,q:search});}}><label className="search-field"><Search size={17} aria-hidden="true"/><input type="search" maxLength={100} value={search} onChange={e=>setSearch(e.target.value)} placeholder="Membro ou competência" aria-label="Buscar membro ou competência em todos os resultados"/></label><button className="button compact secondary" type="submit">Buscar</button></form><span className="count-label">{total} registros · {filterPeriodLabel(filters)}</span><button className="icon-button" type="button" onClick={resource.reload} aria-label="Atualizar mensalidades"><RefreshCw size={19}/></button></header>
  <p className="due-explanation">“Pago · Isento” significa mensalidade regularizada sem entrada de dinheiro. A coluna Recebido mostra somente pagamentos efetivos destinados a ela.</p>
  {resource.loading?<Loading/>:resource.data?.items?.length?<DuesTable rows={resource.data.items} user={user} onDetails={row=>setModal({detailId:row.id})} onAction={(action,row)=>setModal({action:{...action,context:`${row.memberName} · ${competenceLabel(row.competence)} · valor original ${money(row.amountCents)}`},row})}/>:<Empty title="Nenhuma mensalidade para estes filtros">Selecione outro mês, ano ou Todo o histórico. Meses sem registro não são exibidos como pagos nem gerados automaticamente.</Empty>}
  <footer className="table-pagination"><span>Página {page} de {pages}</span><div><button className="icon-button" aria-label="Página anterior" disabled={page<=1||resource.loading} onClick={()=>setPage(Math.max(1,page-1))}><ChevronLeft/></button><button className="icon-button" aria-label="Próxima página" disabled={page>=pages||resource.loading} onClick={()=>setPage(page+1)}><ChevronRight/></button></div></footer></section>
  {modal?.detailId&&<DueDetails id={modal.detailId} onClose={close}/>}{modal?.action&&<ActionForm action={modal.action} row={modal.row} onClose={close} onSaved={saved}/>}
 </>;
}
