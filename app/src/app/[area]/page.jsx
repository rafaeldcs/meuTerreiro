'use client';
import {useParams} from 'next/navigation';
import ModuleScreen from '@/components/Administration';
import DuesScreen from '@/components/Dues';
import {MODULES} from '@/lib/modules.mjs';
import {Empty} from '@/components/UI';
export default function AdministrativeArea(){const {area}=useParams();const module=MODULES[area];return area==='mensalidades'?<DuesScreen/>:module?<ModuleScreen key={area} module={module}/>:<Empty title="Área não encontrada">Use o menu para acessar as áreas disponíveis para seu perfil.</Empty>;}
