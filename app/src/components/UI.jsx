'use client';
import Link from 'next/link';
import { useState, useEffect, useCallback, useRef } from 'react';
import { CalendarDays, ChevronRight, Inbox } from 'lucide-react';
import { api } from '@/lib/api.mjs';
import { formatDate, STATUS_LABELS } from '@/lib/presentation.mjs';
export function useResource(path) {
  const [data,setData]=useState(null),[error,setError]=useState(''),[loading,setLoading]=useState(true);
  const sequence=useRef(0),alive=useRef(true);
  const reload=useCallback(async()=>{
    const request=++sequence.current;
    try {setError('');const value=await api(path);if(alive.current&&sequence.current===request)setData(value);return value;}
    catch(e){if(alive.current&&sequence.current===request)setError(e.message);return null;}
    finally{if(alive.current&&sequence.current===request)setLoading(false);}
  },[path]);
  useEffect(()=>{alive.current=true;setData(null);setLoading(true);void reload();return()=>{alive.current=false;sequence.current++;};},[reload]);
  return {data,setData,error,loading,reload};
}
export function ErrorNotice({children}) {return children ? <div className="notice error" role="alert">{children}</div>:null;}
export function Loading(){return <div className="loading" role="status">Carregando informações…</div>;}
export function Heading({eyebrow='ORGANIZAÇÃO DA CASA',title,children,action}){return <header className="page-heading"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1>{children&&<p className="description">{children}</p>}</div>{action}</header>;}
export function Empty({title,children,action}){return <section className="empty card"><Inbox size={32} aria-hidden="true"/><h2>{title}</h2><p>{children}</p>{action}</section>;}
export function CleaningCard({item}) {return <Link className="card cleaning-card" href={`/limpezas/${item.id}`}><div className="icon-box"><CalendarDays size={23}/></div><div className="grow"><span className={`badge ${item.status==='Cancelled'?'muted':''}`}>{STATUS_LABELS[item.status] || item.status}</span><h2>{item.title}</h2><p>{formatDate(item.startsAt)} · {item.area}</p><div className="card-meta"><span>{item.mode==='General'?'Mutirão geral':`Equipe-alvo: ${item.target}`}</span><span>{item.confirmed}/{item.assigned} confirmados</span></div></div><ChevronRight size={19} aria-hidden="true"/></Link>;}
