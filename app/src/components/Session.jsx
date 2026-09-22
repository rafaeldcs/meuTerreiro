'use client';
import { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { api, post } from '@/lib/api.mjs';
const Context = createContext(null);
export function SessionProvider({ children }) {
  const [user, setUser] = useState(null); const [loading, setLoading] = useState(true); const [error, setError] = useState('');
  const refresh = useCallback(async () => {
    try { setError(''); setUser(await api('/auth/me')); }
    catch(e) { setUser(null); if(e.status !== 401) setError('Não foi possível conectar ao servidor.'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { refresh(); const expired=()=>{setUser(null);setError('');}; window.addEventListener('session-expired',expired); return ()=>window.removeEventListener('session-expired',expired); }, [refresh]);
  return <Context.Provider value={{user,setUser,loading,error,refresh}}>{children}</Context.Provider>;
}
export function useSession() { return useContext(Context); }
export function useSessionGuard() {
  const session=useSession(); const path=usePathname(); const router=useRouter();
  useEffect(()=>{
    if(session.loading || session.error) return;
    if(!session.user && !['/entrar','/recuperar-acesso'].includes(path)) router.replace('/entrar');
    else if(session.user?.mustChangePassword && path !== '/primeiro-acesso') router.replace('/primeiro-acesso');
    else if(session.user && !session.user.mustChangePassword && ['/entrar','/primeiro-acesso','/recuperar-acesso'].includes(path)) router.replace('/');
  },[session.user,session.loading,session.error,path,router]);
  return session;
}
export async function logout() {
  await post('/auth/logout');
  if ('serviceWorker' in navigator) {
    const reg=await navigator.serviceWorker.getRegistration();
    const sub=await reg?.pushManager?.getSubscription();
    await sub?.unsubscribe().catch(()=>{});
    reg?.active?.postMessage({type:'LOGOUT'});
  }
  // Full navigation clears in-memory views from the previous identity.
  window.location.replace('/entrar');
}
