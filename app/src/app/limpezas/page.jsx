'use client';
import Link from 'next/link';
import { Plus } from 'lucide-react';
import { useSession } from '@/components/Session';
import { useResource, Heading, CleaningCard, Loading, ErrorNotice, Empty } from '@/components/UI';
export default function Cleanings(){const{user}=useSession();const{data,error,loading}=useResource('/cleanings');const manage=['Admin','Coordinator'].includes(user?.role);return <><Heading title="Limpeza e escalas" action={manage&&<Link className="button" href="/limpezas/nova"><Plus size={18}/> Nova limpeza</Link>}>Equipes, mutirões e o cuidado com cada espaço da casa.</Heading><ErrorNotice>{error}</ErrorNotice>{loading?<Loading/>:<div className="stack">{data?.map(item=><CleaningCard key={item.id} item={item}/>)}{!data?.length&&!error&&<Empty title="Vamos organizar a primeira limpeza?">{manage?'Cadastre os membros e publique uma equipe ou um mutirão.':'Quando você for escalado, sua atividade aparecerá aqui.'}</Empty>}</div>}</>;}
