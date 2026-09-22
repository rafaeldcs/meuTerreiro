#!/usr/bin/env bash
# Offline-consistent backup. Run during an authorized maintenance window.
set -euo pipefail
umask 077
cd "$(dirname "$0")/.."
for bin in docker age python3 tar sha256sum; do command -v "$bin" >/dev/null || { echo "Falta instalar $bin." >&2; exit 1; }; done
: "${AGE_RECIPIENT:?Defina a chave PUBLICA age do responsável pelo backup. Não é a chave privada.}"
OUT="${1:?Uso: AGE_RECIPIENT=age1... bash scripts/backup.sh /pasta/protegida/externa}"
OUT="$(realpath -m "$OUT")"; ROOT="$(pwd)"
case "$OUT/" in "$ROOT/"*) echo 'O backup deve ficar fora da pasta do código.' >&2; exit 1;; esac
[[ -f .env && -f .secrets/mongo-key ]] || { echo 'Configuração ausente.' >&2; exit 1; }
mkdir -p "$OUT"; chmod 700 "$OUT"
TMP="$(mktemp -d)"; restart=()
cleanup(){ rc=$?; trap - EXIT; rm -rf "$TMP"; if ((${#restart[@]})); then docker compose start "${restart[@]}" >/dev/null || echo 'ATENÇÃO: confira e reinicie os serviços interrompidos.' >&2; fi; exit "$rc"; }; trap cleanup EXIT
while IFS= read -r service; do case "$service" in api|app|processor) restart+=("$service");; esac; done < <(docker compose ps --services --status running)
if ((${#restart[@]})); then docker compose stop "${restart[@]}"; fi
# Credentials are resolved inside the container, not printed or expanded in the host command line.
docker compose exec -T mongo sh -ec 'exec mongodump --host localhost --username root --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin --db terreiro_hml --archive --gzip' > "$TMP/database.archive.gz"
mkdir "$TMP/evidence" "$TMP/config"
docker compose cp api:/app/data/evidence/. "$TMP/evidence/"
cp .env "$TMP/config/app.env"; cp .secrets/mongo-key "$TMP/config/mongo-key"
printf '%s\n' 'terreiro-backup-v1' > "$TMP/FORMAT"
python3 - "$TMP" <<'PYCODE'
import hashlib,json,sys
from pathlib import Path
p=Path(sys.argv[1]); rows={}
for f in sorted(p.rglob('*')):
 if f.is_symlink():raise SystemExit('Link simbólico não permitido no backup.')
 if f.is_file():rows[str(f.relative_to(p))]=hashlib.file_digest(f.open('rb'),'sha256').hexdigest()
(p/'manifest.json').write_text(json.dumps(rows,indent=2))
PYCODE
NAME="terreiro-$(date -u +%Y%m%dT%H%M%SZ)-$RANDOM.tar.age"
tar -C "$TMP" -cf - . | age -r "$AGE_RECIPIENT" -o "$OUT/$NAME.part"
mv "$OUT/$NAME.part" "$OUT/$NAME"
sha256sum "$OUT/$NAME" > "$OUT/$NAME.sha256"
printf 'Backup criptografado criado: %s\n' "$OUT/$NAME"
echo 'Backup gerado não comprova restauração. Execute restore-drill.sh em ambiente isolado.'
