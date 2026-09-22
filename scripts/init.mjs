import {existsSync,mkdirSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {join} from 'node:path';
import {randomBytes,createECDH} from 'node:crypto';
const root=fileURLToPath(new URL('../',import.meta.url));
const args=process.argv.slice(2);
function argument(name,fallback){const index=args.indexOf(name);if(index<0)return fallback;if(!args[index+1]||args[index+1].startsWith('--'))throw new Error(`Falta valor para ${name}`);return args[index+1];}
const host=argument('--host',''); const port=Number(argument('--port','8080'));
if(host&&!/^(?=.{4,253}$)[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\.[a-z]{2,}$/i.test(host))throw new Error('Informe somente o domínio, sem https:// ou caminho.');
if(!Number.isInteger(port)||port<1024||port>65535)throw new Error('Porta inválida.');
const env=join(root,'.env');
if(existsSync(env))throw new Error('.env já existe. Não sobrescreverei credenciais nem chaves. Edite a configuração existente com cuidado.');
mkdirSync(join(root,'.secrets'),{recursive:true,mode:0o700});
const key=createECDH('prime256v1');key.generateKeys();
writeFileSync(join(root,'.secrets','mongo-key'),randomBytes(756).toString('base64'),{mode:0o600,flag:'wx'});
const origin=host?`https://${host}`:`http://localhost:${port}`;
const config={
 APP_HOST:host||'localhost',APP_ORIGIN:origin,APP_ENVIRONMENT:host?'Staging':'Development',BIND_ADDRESS:'127.0.0.1',HTTP_PORT:port,
 MONGO_ROOT_PASSWORD:randomBytes(32).toString('hex'),MONGO_APP_PASSWORD:randomBytes(32).toString('hex'),
 PROCESSOR_KEY:randomBytes(32).toString('hex'),BOOTSTRAP_LOGIN:'admin',BOOTSTRAP_PASSWORD:randomBytes(24).toString('base64url'),
 WEB_PUSH_ENABLED:host?'true':'false',VAPID_SUBJECT:host?origin:'https://localhost',
 VAPID_PUBLIC_KEY:key.getPublicKey().toString('base64url'),VAPID_PRIVATE_KEY:key.getPrivateKey().toString('base64url')
};
writeFileSync(env,'# Homologação. Nunca versionar este arquivo.\n'+Object.entries(config).map(([k,v])=>`${k}=${v}`).join('\n')+'\n',{mode:0o600,flag:'wx'});
console.log(`Configuração criada: ${env}
Usuário inicial: admin. A senha temporária está em BOOTSTRAP_PASSWORD no arquivo local .env.
Endereço configurado: ${origin}
Nada foi publicado por este comando. Continue com Docker e o proxy HTTPS.`);
