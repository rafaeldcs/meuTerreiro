'use client';
import Link from 'next/link';
import {useCallback,useState} from 'react';
import {useSession} from './Session';
import {useResource,ErrorNotice,Loading} from './UI';
import {Dialog,ActionForm} from './Administration';
import {has,safeDisplay} from '@/lib/administration.mjs';
import {competenceLabel} from '@/lib/dues.mjs';
import {exemptionState,canEndExemption,nextGrantPeriod,grantExemptionAction,endExemptionAction,withdrawExemptionAction,EXEMPTION_EXCEPTION_LABELS} from '@/lib/member-exemptions.mjs';

export function MemberExemptionsContent({data,user,onGrant,onEnd,onWithdraw}){
 const rules=data.rules||[];const active=rules.find(x=>x.id===data.currentId);
 const writable=has(user,'finance.write');const grantFrom=nextGrantPeriod(rules,data.currentPeriod);
 return <div className="member-exemptions"><div className="due-detail-title"><div><h3>{data.memberName}</h3><p>Isenção vinculada a este cadastro, não a um título ou perfil de acesso.</p></div><span className={'badge '+(active?'exempt':'muted')}>{active?'Isento de mensalidade':'Sem isenção vigente'}</span></div>
  <p className="notice">Com a regra aprovada, as mensalidades na vigência são regularizadas automaticamente como <strong>Pago · Isento</strong>, sem entrada de dinheiro. O motivo permanece nos detalhes de cada mês.</p>
  {writable&&grantFrom&&data.memberActive&&data.isMember&&<button className="button" onClick={()=>onGrant(grantFrom)}>{grantFrom>data.currentPeriod?'Cadastrar próxima isenção':'Isentar este médium'}</button>}
  {!rules.length&&<p>Nenhuma isenção recorrente cadastrada para este médium.</p>}
  <div className="due-history">{rules.map(rule=>{const status=exemptionState(rule,data.currentPeriod);return <article className="due-history-item" key={rule.id}>
   <span className={'badge '+status.tone}>{status.label}</span><h4>Desde {competenceLabel(rule.effectiveFrom)}</h4>
   <p className="due-reason"><strong>Motivo:</strong> {rule.reason}</p>
   <p>{rule.effectiveTo?'Até '+competenceLabel(rule.effectiveTo):'Sem término definido.'}</p>
   {rule.approvedAt&&<p>Aprovação: {safeDisplay(rule.approvedAt,'datetime')}</p>}
   {rule.endedFrom&&<p><strong>Cobrança normal a partir de {competenceLabel(rule.endedFrom)}.</strong> Motivo: {rule.endReason}</p>}
   {rule.pendingEnd&&<p className="notice">Encerramento aguardando revisão. A regra vigente ainda não mudou.</p>}
   {rule.appliedExistingCount>0&&<p>{rule.appliedExistingCount} mensalidade(s) já gerada(s) regularizada(s) na aprovação.</p>}
   {!!rule.exceptions?.length&&<details><summary>Mensalidades preservadas na aprovação ({rule.exceptions.length})</summary>{rule.exceptions.map(x=><p key={x.dueId}>{competenceLabel(x.competence)}: {EXEMPTION_EXCEPTION_LABELS[x.code]||'Revisão necessária.'}</p>)}</details>}
   <div className="button-row">{writable&&canEndExemption(rule,data.currentPeriod)&&<button className="button secondary" onClick={()=>onEnd(rule)}>Encerrar isenção</button>}
    {writable&&rule.state==='Pending'&&rule.requestedBy===user?.id&&<button className="button secondary" onClick={()=>onWithdraw(rule)}>Retirar solicitação</button>}</div>
  </article>;})}</div>
  <p className="field-help">Esta regra não dispensa limpeza, não muda permissões e não apaga mensalidades anteriores à vigência. Uma isenção individual antiga não é convertida automaticamente em benefício recorrente.</p>
  <Link className="button secondary" href="/mensalidades">Consultar mensalidades e filtros de meses</Link>
 </div>;
}
export default function MemberExemptions({member,onClose}){
 const {user}=useSession();const r=useResource('/members/'+encodeURIComponent(member.id)+'/exemptions');
 const [action,setAction]=useState(null),[message,setMessage]=useState('');const closeAction=useCallback(()=>setAction(null),[]);
 const saved=async()=>{setMessage('Solicitação registrada. Consulte a aprovação; enviar o pedido não ativa nem encerra a isenção.');await r.reload();};
 if(action)return <ActionForm action={action.spec} row={action.row} onClose={closeAction} onSaved={saved}/>;
 return <Dialog title="Mensalidade do médium" onClose={onClose} wide><div className="modal-body"><ErrorNotice>{r.error}</ErrorNotice>{message&&<p className="notice" role="status">{message}</p>}{r.loading?<Loading/>:r.data?<MemberExemptionsContent data={r.data} user={user}
  onGrant={from=>setAction({spec:grantExemptionAction(member,from),row:{}})}
  onEnd={row=>setAction({spec:endExemptionAction(row,r.data.currentPeriod),row})}
  onWithdraw={row=>setAction({spec:withdrawExemptionAction(),row})}/>:<button className="button" onClick={r.reload}>Tentar novamente</button>}</div></Dialog>;
}
