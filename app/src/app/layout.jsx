import './globals.css';
import { SessionProvider } from '@/components/Session';
import Frame from '@/components/Frame';
import PwaBoot from '@/components/PwaBoot';
export const metadata={title:'Tenda d’Água | Gestão da casa',description:'Aplicativo de gestão administrativa. Versão de homologação.',manifest:'/manifest.webmanifest',robots:{index:false,follow:false},appleWebApp:{capable:true,statusBarStyle:'default',title:'Tenda d’Água'},icons:{apple:'/icons/apple-touch-icon.png',icon:'/icons/icon-192.png'}};
export const viewport={width:'device-width',initialScale:1,themeColor:'#155d54',viewportFit:'cover'};
export default function Layout({children}){return <html lang="pt-BR"><body><SessionProvider><PwaBoot/><Frame>{children}</Frame></SessionProvider></body></html>;}
