'use client';
import {useEffect} from 'react';
import {useSession} from './Session';
import {isMobile} from '@/lib/presentation.mjs';
import {api,post} from '@/lib/api.mjs';
export default function PwaBoot(){const{user}=useSession();useEffect(()=>{
 const install=e=>{e.preventDefault();window.terreiroInstallPrompt=e;};
 const installed=()=>{window.terreiroInstallPrompt=null;};
 window.addEventListener('beforeinstallprompt',install);window.addEventListener('appinstalled',installed);
 if('serviceWorker'in navigator&&window.isSecureContext) navigator.serviceWorker.register('/sw.js',{scope:'/',updateViaCache:'none'}).catch(()=>{});
 return()=>{window.removeEventListener('beforeinstallprompt',install);window.removeEventListener('appinstalled',installed);};
 },[]);
 useEffect(()=>{if(!user||user.mustChangePassword||!isMobile(navigator.userAgent)||!('Notification'in window)||Notification.permission!=='granted'||!('serviceWorker'in navigator))return;
 let active=true;
 (async()=>{const config=await api('/push/config');if(!config.enabled)return;const reg=await navigator.serviceWorker.ready;const sub=await reg.pushManager.getSubscription();if(active&&sub)await post('/push/subscribe',sub.toJSON());})().catch(()=>{});
 return()=>{active=false;};},[user]);
 return null;}
