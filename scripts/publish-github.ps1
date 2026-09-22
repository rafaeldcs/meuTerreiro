param([Parameter(Mandatory=$true)][string]$Repository)
$ErrorActionPreference = "Stop"
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw "Use proprietario/repositorio." }
Set-Location (Split-Path $PSScriptRoot -Parent)
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "Instale e autentique o GitHub CLI antes de publicar." }
& gh auth status
if ($LASTEXITCODE -ne 0) { throw "Autenticação GitHub necessária. Execute gh auth login no seu computador." }
if (Test-Path ".git") { throw "Já existe um repositório Git local. Confira o remoto e publique manualmente; nenhum remoto foi alterado." }
& git init -b dev
if ($LASTEXITCODE -ne 0) { throw "Falha ao iniciar o repositório local." }
& git add .
$staged = & git diff --cached --name-only
if ($staged | Where-Object { $_ -match '(^|/)(\.env|\.secrets)(/|$|\.)' }) { throw "Arquivo sensível detectado no índice. Publicação interrompida." }
& git commit -m "feat: initial terreiro PWA, cleaning workflows and private notification center"
if ($LASTEXITCODE -ne 0) { throw "Configure a identidade Git no seu computador e tente publicar manualmente." }
& gh repo create $Repository --private --source . --remote origin --push
if ($LASTEXITCODE -ne 0) { throw "Publicação não concluída. O código permanece local; confira o resultado do GitHub CLI." }
