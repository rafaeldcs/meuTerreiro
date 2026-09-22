'use client';
import Link from 'next/link';
import { useState } from 'react';
import { Waves, ShieldCheck } from 'lucide-react';
import { useSession } from '@/components/Session';
import { ErrorNotice } from '@/components/UI';
import { post } from '@/lib/api.mjs';
export default function Login(){
 const {setUser}=useSession(); const [error,setError]=useState(''),[busy,setBusy]=useState(false);
 async function submit(e){e.preventDefault();const form=new FormData(e.currentTarget);setBusy(true);setError('');try{setUser(await post('/auth/login',{login:form.get('login'),password:form.get('password')}));}catch(ex){setError(ex.message);}finally{setBusy(false);}}
 return <section className="auth-card card"><div className="brand-icon large"><Waves size={34}/></div><p className="eyebrow">CENTRO DE UMBANDA</p><h1>Tenda d’Água</h1><p className="description">A organização da nossa casa,<br/>perto de você.</p><div className="notice">Homologação · use somente dados de teste.</div><form onSubmit={submit}><label>Usuário<input name="login" autoComplete="username" autoCapitalize="none" maxLength={50} required placeholder="Seu usuário na casa"/></label><label>Senha<input name="password" type="password" autoComplete="current-password" maxLength={128} required placeholder="Sua senha"/></label><ErrorNotice>{error}</ErrorNotice><button className="button full" disabled={busy}>{busy?'Entrando…':'Entrar no aplicativo'}</button></form><Link className="text-button" href="/recuperar-acesso">Recuperar acesso com código</Link><p className="footnote"><ShieldCheck size={16}/> O acesso é individual. Para ativação ou recuperação, procure a administração presencialmente.</p></section>;
}
