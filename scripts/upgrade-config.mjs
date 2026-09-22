// Adds only the new processor credential to an existing local configuration.
import {readFileSync,appendFileSync} from 'node:fs';
import {randomBytes} from 'node:crypto';
const file=new URL('../.env',import.meta.url);const text=readFileSync(file,'utf8');
if(!/^PROCESSOR_KEY=.{32,}$/m.test(text)){
 if(/^PROCESSOR_KEY=/m.test(text))throw Error('PROCESSOR_KEY já existe, mas é inválida. Revise localmente sem compartilhar o segredo.');
 appendFileSync(file,'\nPROCESSOR_KEY='+randomBytes(32).toString('hex')+'\n',{mode:0o600});
 console.log('Chave privada do processador acrescentada. Nenhuma credencial anterior foi alterada.');
}else console.log('Configuração já possui chave de processador. Nada alterado.');
