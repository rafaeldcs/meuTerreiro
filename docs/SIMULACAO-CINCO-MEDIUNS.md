# Simulação de cinco médiuns — 29/09/2026

Ambiente isolado: http://localhost:3180. Casa do banco: `centro-simulacao-cinco-mediuns`; marca visual original: Tenda d’Água. Nenhuma pessoa real, cobrança bancária ou mensagem externa foi usada.

## Cenário

| Pessoa fictícia | Papel técnico | Mensalidade |
|---|---|---|
| Ana | Member | R$100 pagos |
| Bruno | Treasury | R$50 pagos, R$50 em aberto |
| Carla | Stock | R$100 em aberto |
| Diego | Secretary | R$100 isentos com revisão independente |
| Elisa | Coordinator | R$100 pagos; recebimento de R$120 |

Exatamente cinco membros ativos. Administradores, auditor e aprovador financeiro são operadores fora do quadro de médiuns. Foram exercitados os sete papéis existentes; não existe papel separado “Dirigente”. Coordenação e administração são permissões técnicas, sem inferir hierarquia religiosa.

R$500 nominais = R$250 pagos + R$100 isentos + R$150 em aberto. R$20 excedentes permanecem disponíveis. Uma entrada extra de R$10, saída de R$40 e transferência interna de R$50 deixam R$240 no total das contas.

Acessos aleatórios somente em `.secrets/simulation-access.json` neste computador, fora do Git e da galeria. Não usar em produção.

## Validação executada

| Camada | Resultado |
|---|---|
| JavaScript | 195 testes aprovados |
| C# | 138 testes aprovados; build/publish .NET 10 executado |
| Processador | 18 testes Python aprovados |
| Interface | Build Next executado |
| Integração preexistente | 145 checks HTTP em banco novo separado |
| Cinco médiuns | 277 checks HTTP e de invariantes, com MongoDB replica set real |
| Complemento | 51 checks de arquivos privados, relatórios/CSV, campanha, escalas, notificações e recibos |
| Navegador | 505 checks, 37 rotas, sete perfis, 154 combinações permitidas; desktop e celular; 308 capturas |
| Interações | Seis checks e cinco capturas de criação/conclusão de tarefa, detalhes, isenção e acesso negado |
| LocalAuthor | 220 testes aprovados em Linux, sem pulos |

Os checks HTTP incluem requisições auxiliares/polling; as contagens não são requisitos independentes nem percentual de cobertura. Os JSONs e capturas ficam em `artifacts/simulation/`, com falhas anteriores preservadas. A galeria é `artifacts/simulation/index.html`.

## Regras exercitadas

- Autenticação, troca obrigatória de senha, invalidação de sessão após mudança de papel e matriz de autorização. Administrador comum não ganha tesouraria automaticamente.
- Revisão por outra pessoa; negação de autoaprovação e de benefício próprio. Mensalidade privada e documentos não ficam disponíveis a outro médium.
- Geração mensal idempotente, pagamento parcial, isenção recorrente, saldo excedente, concorrência de recebimento e referências duplicadas.
- Despesa aprovada antes do pagamento; transferência conserva saldo; caixa contado e revisado; fechamento bloqueia novos lançamentos.
- Dez unidades em estoque; quatro entregues; duas consumidas, uma devolvida e uma perdida deixam sete. Reserva excessiva, prestação excessiva e lote vencido são rejeitados.
- Compra com recebimentos parciais, doação material, empréstimo/devolução de patrimônio, manutenção, tarefas e orçamento.
- Leitura, ciência, confirmação, presença e tarefa são distintos. Troca exige aceite/aprovação; reagendamento invalida confirmação antiga; dispensa é separada e série pode ser interrompida.
- Upload não quita. Processador e ClamAV reais analisaram imagens sintéticas. Download usa controle positivo: mesma rota abre para secretaria e nega ao médium. Liberação técnica não autentica pagamento.
- Conciliação CSV, recuperação de acesso, isenções e cancelamento de limpeza também foram exercitados pelo smoke preexistente.

## Autoria e ensino

Codex investigou e escreveu os roteiros. Antes da preferência reforçada pelo usuário, corrigiu diretamente o erro C# `CS8604` e a sintaxe do Caddyfile. Essas duas correções não são atribuídas à IA local.

O laboratório LocalAuthor executou os workers e produziu as capturas; seus passos e asserções foram escritos por Codex. Um especialista neural separado aprendeu a selecionar a próxima etapa de QA: 17/256 antes, 256/256 depois em combinações sintéticas reservadas. Diante de uma falha real normalizada, propôs `INVESTIGATE_FAILURE`.

Isso não prova autoria de código. Após a orientação de que o saravaAPP deve ser corrigido pela IA local, problemas Caddy e C# foram enviados ao modelo conversacional sem entregar a correção pronta. Propostas inválidas ficam reprovadas, sem aplicação. Consulte o relatório de autoria no LocalAuthor; não confundir execução de roteiro, consulta de notas, classificação de etapas e geração de código.

Lições foram importadas no projeto MeuTerreiro do servidor LocalAuthor. Notas importadas são memória de consulta, não treino automático. Pesos/corpus ficam fora do Git em `%LOCALAPPDATA%\LocalAuthor\models\terreiro-qa-20260929`. O Transformer conversacional não foi substituído nem este especialista conectado automaticamente ao chat.

## Repetir

1. Revise os repositórios e confirme Docker ativo. Execute código do projeto somente no laboratório isolado.
2. Em checkout novo, execute `scripts/init.mjs --port 3180` pelo Node do laboratório montando apenas esse checkout. Ele recusa sobrescrever `.env`. Preserve `APP_ORIGIN=http://localhost:3180`.
3. Copie o plano do especialista local para `artifacts/local-ai-plan.json`. A imagem revisada `localauthor-functional-lab:1` precisa estar disponível.
4. Use um nome Compose inédito. Pare a instância anterior com `down`, sem `-v`, preservando os volumes. Suba `docker compose -p NOME -f compose.yaml -f compose.simulation.yaml up -d --build`.
5. Execute `scripts/simulate-five.mjs` pelo Node do laboratório com `--network container:NOME-gateway-1`, raiz somente leitura, `/tmp` temporário limitado, capabilities removidas, `no-new-privileges`, memória/PIDs limitados e checkout em `/workspace`. O script exige bootstrap novo; não repetir no mesmo banco.
6. Após a janela do limitador de login (60 segundos), execute `scripts/simulate-five-followup.mjs` na mesma sandbox. Não desative o limitador para facilitar testes.
7. Rode os workers `scripts/run-terreiro-browser.cjs` e `scripts/run-terreiro-interactions.cjs` do LocalAuthor. Monte seu repositório em `/workspace:ro`, somente o arquivo de acessos em `/input/access.json:ro`, plano em `/input/plan.json:ro` e evidências em `/output`. Não monte socket Docker nem a pasta pessoal.
8. Rode `scripts/terreiro-gallery.py /output` pelo Python do laboratório. Revise JSONs/imagens, preserve falhas e importe lições no LocalAuthor. Não treine com segredos nem use teste reservado como treino.
9. Para novas correções do saravaAPP, exija proposta original da IA local, diff revisado e teste de regressão. Codex revisa e valida; proposta inválida não é aplicada nem reescrita silenciosamente.
10. Commit/push somente de fontes e relatórios sanitizados. Credenciais, bancos, capturas e modelos não entram no Git.

Instância desta entrega: `meuterreiro-sim-five-final`. Retomar os mesmos dados: `docker compose -p meuterreiro-sim-five-final -f compose.yaml -f compose.simulation.yaml up -d --no-build`. Não executar o seed novamente nessa instância.

## Limites

Não é “100% de todos os casos possíveis”. Banco/Pix real, push em celulares físicos, restauração, carga sustentada, acessibilidade completa, todos os navegadores e todas as combinações de ajustes/devoluções/adiantamentos ainda não foram homologados. Recursos ausentes em `STATUS.md` continuam ausentes. Navegar por 37 telas não comprova cada operação de cada tela. Não houve produção nem novo instalador nesta tarefa.
