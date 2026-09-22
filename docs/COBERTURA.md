> Atualização 0.2.2: incluída isenção recorrente por médium, com motivo, vigência, aprovação e encerramento. Leia `ISENCAO-NO-CADASTRO.md`. Não altera a situação dos demais bloqueadores registrados neste documento.

# Catálogo e rastreabilidade de implementação

Este catálogo lista rotas e recursos existentes na fonte, não aprovação integral de requisitos. Os comandos de `app/src/lib/modules.mjs` são ligados aos serviços C# pelo dispatcher da API. Os testes JS verificam a existência textual desses contratos; **não executam o serviço C#**.

| Rota | Área | Recurso autorizado |
|---|---|---|
| `/mensalidades` | Mensalidades | `dues` |
| `/regras` | Regras de contribuição | `rules` |
| `/financeiro` | Contas e movimentos | `accounts` |
| `/movimentos` | Movimentações financeiras | `ledger` |
| `/recebimentos` | Recebimentos | `receipts` |
| `/recibos` | Meus recibos | `allocations` |
| `/comprovantes` | Comprovantes | `evidence` |
| `/revisoes` | Esclarecimentos | `reviews` |
| `/despesas` | Despesas e reembolsos | `expenses` |
| `/aprovacoes` | Aprovações | `approvals` |
| `/devolucoes` | Devoluções | `refunds` |
| `/caixa` | Caixa físico | `cash` |
| `/conciliacao` | Conciliação | `statements` |
| `/extratos` | Importações de extrato | `statement-batches` |
| `/fechamentos` | Fechamento mensal | `closings` |
| `/estoque` | Estoque | `items` |
| `/lotes` | Saldos e lotes | `lots` |
| `/reservas` | Materiais por atividade | `reservations` |
| `/movimentos-estoque` | Histórico de estoque | `stock-movements` |
| `/compras` | Compras | `purchases` |
| `/fornecedores` | Fornecedores | `suppliers` |
| `/doacoes` | Doações | `donations` |
| `/campanhas` | Campanhas e fundos | `funds` |
| `/eventos` | Eventos | `events` |
| `/patrimonio` | Patrimônio | `assets` |
| `/manutencao` | Manutenção | `maintenance` |
| `/tarefas` | Tarefas administrativas | `tasks` |
| `/documentos` | Documentos da casa | `documents` |
| `/arquivos-documentos` | Arquivos institucionais | `document-files` |
| `/orcamento` | Orçamento | `budgets` |
| `/comunicados` | Comunicados | `announcements` |
| `/recorrencias` | Escalas recorrentes | `series` |
| `/trocas` | Trocas de escala | `swaps` |
| `/acessos` | Permissões e vínculos | `access-changes` |
| `/auditoria` | Auditoria | `audit` |
| `/pagamentos` | Pagamentos Pix | `payment-intents` |
| `/observacoes-bancarias` | Verificações do provedor | `bank-observations` |

Outras telas: início, login/troca de senha, membros, listas/detalhe/publicação de limpeza, configurações, notificações, relatório financeiro, recibo individual e recuperação de acesso.

## Relação com os grupos de requisitos da v0.3

| Grupo | Cobertura escrita | Verificação ainda necessária |
|---|---|---|
| MEN / FIN | Mensalidades, recebimentos, alocações, transações, ajustes, aprovação e fechamento. | Compilação, xUnit e banco real; casos de rateio/reversão/concorrência. |
| ARQ / REV | Quarentena, hashes, análise estrutural e texto de PDF; revisão com mensagens protegidas. | Scanner real, carga, arquivos especiais; OCR e assinatura criptográfica não implementados. |
| PIX / CON | Efí Sandbox opcional e conciliação CSV/manual. | Credenciais e teste com provedor; não existe Pix real homologado. |
| LIMP | Equipe-alvo/mutirão, versão, resposta, presença, tarefas, troca e recorrência semanal. | Revisão do escopo completo, grupos/rodízio/voluntariado, materiais, eventos e concorrência. |
| NOT | Central móvel, registro durável, transporte separado, permissões e preferências. | Aparelhos reais, reentrega, expiração, uso compartilhado e indisponibilidade prolongada. |
| SEG / PRIV | Separação de funções, recuperação, reautenticação, registros e arquivos privados. | MFA, retenção, auditoria externa, restauração, autorização integral e revisão de segurança. |

Os cenários T01–T70 em `ESPECIFICACAO-v0.3-backlog.md` permanecem como a referência de aceitação. Nem 138 testes JS nem 35 medições de layout equivalem a executar esses 70 cenários de ponta a ponta.

## Adendo 0.2.1-alpha.1

- `GET /api/dues`: ano/mês/situação/pesquisa, autorização, paginação e anos existentes.
- `GET /api/dues/{id}`: dados da mensalidade, motivo da isenção, pagamentos próprios e histórico.
- `POST /api/finance/due.exemption.request`: motivo único, cálculo do ajuste no servidor e solicitação para revisão.
- `POST /api/finance/approval.review`: aplica isenção sem pagamento/caixa, conserva revisão independente e notifica no aplicativo.
- `Dues.jsx`: visão mês corrente, filtros, listas/cartões, detalhes e integração com o formulário existente.
- `DueRulesTests.cs`: testes de regras C# acrescentados; não executados neste ambiente.
- `scripts/smoke.mjs`: cenários HTTP de isenção, filtros, autorização e caixa inalterado acrescentados; não executados.
