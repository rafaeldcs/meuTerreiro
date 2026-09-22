#!/usr/bin/env bash
# Never overwrites the running project. Restores ONLY into a newly created directory/project.
set -euo pipefail
umask 077
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ARCHIVE="$(realpath "${1:?Uso: restore-drill.sh backup.tar.age chave-privada.age pasta-nova}")"
IDENTITY="$(realpath "${2:?Informe caminho da identidade age fora do projeto.}")"
TARGET="$(realpath -m "${3:?Informe uma pasta nova e inexistente.}")"
for bin in docker age python3 node tar; do command -v "$bin" >/dev/null || { echo "Falta instalar $bin." >&2; exit 1; }; done
[[ ! -e "$TARGET" ]] || { echo 'Destino já existe. Restauração sobre dados existentes é proibida.' >&2; exit 1; }
case "$TARGET/" in "$ROOT/"*) echo 'Use pasta fora do projeto atual.' >&2; exit 1;; esac
[[ "${ACK_RESTORE_DRILL:-}" == 'yes' ]] || { echo 'Defina ACK_RESTORE_DRILL=yes para autorizar criar containers e volumes de teste.' >&2; exit 1; }
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
age -d -i "$IDENTITY" -o "$TMP/backup.tar" "$ARCHIVE"
python3 - "$TMP" <<'PYCODE'
import hashlib,json,sys,tarfile
from pathlib import Path
p=Path(sys.argv[1]); data=p/'contents';data.mkdir()
with tarfile.open(p/'backup.tar') as t:
 for item in t.getmembers():
  if not(item.isfile() or item.isdir()):raise SystemExit('Backup contém tipo de arquivo não permitido.')
 t.extractall(data,filter='data')
if (data/'FORMAT').read_text().strip()!='terreiro-backup-v1':raise SystemExit('Formato incompatível.')
manifest=json.loads((data/'manifest.json').read_text())
actual={str(f.relative_to(data)) for f in data.rglob('*') if f.is_file() and f.name!='manifest.json'}
if actual!=set(manifest):raise SystemExit('Lista de arquivos divergente.')
for name,expected in manifest.items():
 f=data/name
 if hashlib.file_digest(f.open('rb'),'sha256').hexdigest()!=expected:raise SystemExit('Integridade inválida: '+name)
print('Arquivo autenticado e integridade interna conferida; banco ainda não restaurado.')
PYCODE
mkdir -p "$TARGET"
tar -C "$ROOT" --exclude='.git' --exclude='.env' --exclude='.secrets' --exclude='node_modules' --exclude='.next' --exclude='__pycache__' -cf - . | tar -C "$TARGET" -xf -
cd "$TARGET"; mkdir -p .secrets
cp "$TMP/contents/config/app.env" .env; cp "$TMP/contents/config/mongo-key" .secrets/mongo-key
NAME="terreiro-restore-$(date -u +%Y%m%d%H%M%S)-$RANDOM"; PORT="${RESTORE_HTTP_PORT:-18088}"
[[ "$PORT" =~ ^[0-9]{4,5}$ ]] && ((PORT>=1024&&PORT<=65535)) || { echo 'Porta de teste inválida.' >&2; exit 1; }
node --input-type=module - "$NAME" "$PORT" <<'JSCODE'
import fs from 'node:fs';
const [name,port]=process.argv.slice(2);let env=fs.readFileSync('.env','utf8');
for(const [k,v]of Object.entries({COMPOSE_PROJECT_NAME:name,HTTP_PORT:port,BIND_ADDRESS:'127.0.0.1',APP_ENVIRONMENT:'Development',APP_ORIGIN:`http://localhost:${port}`,WEB_PUSH_ENABLED:'false'})){
 const re=new RegExp('^'+k+'=.*$','m');env=re.test(env)?env.replace(re,k+'='+v):env+'\n'+k+'='+v;
}
fs.writeFileSync('.env',env,{mode:0o600});
JSCODE
# The new Compose project name gives the drill its own volumes, even on the same host.
docker compose up -d mongo-init
code="$(docker inspect -f '{{if .State.Running}}99{{else}}{{.State.ExitCode}}{{end}}' "$(docker compose ps -a -q mongo-init)" 2>/dev/null || echo 99)"
for i in $(seq 1 60); do [[ "$code" == 0 ]] && break; sleep 3; code="$(docker inspect -f '{{if .State.Running}}99{{else}}{{.State.ExitCode}}{{end}}' "$(docker compose ps -a -q mongo-init)" 2>/dev/null || echo 99)"; done
[[ "$code" == 0 ]] || { echo 'Inicialização do MongoDB do ensaio falhou.' >&2; exit 1; }
docker compose exec -T mongo sh -ec 'exec mongorestore --host localhost --username root --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin --archive --gzip --nsInclude="terreiro_hml.*" --stopOnError' < "$TMP/contents/database.archive.gz"
docker compose build api app
docker compose create --no-deps api
docker compose cp "$TMP/contents/evidence/." api:/app/data/evidence/
# No push and no processor/scanner started in the drill: prevents extra outgoing traffic/costs.
docker compose up -d api app gateway
node scripts/health-check.mjs "http://localhost:$PORT"
printf 'Restauração técnica iniciada no projeto isolado %s, pasta %s.\n' "$NAME" "$TARGET"
echo 'Compare saldos, vínculos e anexos no app. Não execute smoke.mjs contra dados restaurados.'
echo 'Nenhum volume do projeto original foi removido ou alterado; o ensaio NÃO é produção.'
