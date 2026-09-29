# Estado real da versão 0.2.2-alpha.1

## Rodada local de 29/09/2026

Consulte [Simulação de cinco médiuns](SIMULACAO-CINCO-MEDIUNS.md). Nesta rodada foram executados build Next/.NET, 195 testes JavaScript, 138 C#, 18 Python e integração real no Compose descartável. A simulação de cinco médiuns passou 277 checks, com 51 verificações complementares. O navegador percorreu 37 rotas nos sete perfis, desktop/celular, com 505 checks e seis de interações. São evidências locais, não liberação de produção nem cobertura total da especificação. Os limites históricos abaixo permanecem aplicáveis onde não foram explicitamente superados por esta rodada.

Correções de compilação e proxy foram escritas por Codex antes do reforço da preferência de autoria. Novas alterações no saravaAPP devem ser propostas pela IA local e revisadas; não atribuir testes escritos pelo revisor ao modelo.

## Como interpretar

**Escrito** significa que há código de tela, rota e serviço conforme o caso. **Testado** é restrito às verificações efetivamente executadas em `VALIDACAO.md`. **Homologado** exige o fluxo integrado e aprovação dos responsáveis. Esta entrega não foi homologada nem publicada.

## Isenção vinculada ao cadastro do médium — 0.2.2-alpha.1

A regra recorrente agora pertence a um médium cadastrado (`MemberExemption.MemberId`), sem cadastro de títulos. Em **Membros → Isenção de mensalidade**, a equipe autorizada informa o motivo e a primeira competência. O fim é opcional; vazio significa sem término definido. Uma aprovação independente ativa a regra para todas as mensalidades geradas na vigência.

Mensalidades existentes, ainda sem pagamento e em períodos abertos, são regularizadas durante a aprovação. As demais ficam preservadas e identificadas no cadastro para revisão. Nas competências abrangidas, a interface mantém **Pago · Isento** e os detalhes informam a origem no cadastro. Não há entrada de caixa nem criação de recibo de dinheiro.

O encerramento aprovado retoma a cobrança a partir do próximo mês ou posterior e preserva meses anteriores. Mensalidades futuras já isentadas por essa regra têm somente o ajuste dessa origem revertido. Isenções individuais antigas não se tornam benefícios permanentes. A ação por competência permanece como **Isentar somente este mês (exceção)**.

Guia: `docs/ISENCAO-NO-CADASTRO.md`. Resultados desta revisão: **195 testes JavaScript aprovados**, **60 verificações de layout isolado** e **47 arquivos JS/JSX analisados sem erros de sintaxe**. C#, MongoDB, build Next e integração HTTP não foram validados; não é publicação em servidor nem liberação de produção.

## Implementação ampliada em código

| Área | Implementação e limites importantes |
|---|---|
| Cadastros e permissões | Login, senha temporária, recuperação por códigos, criação de membros, segundo administrador inicial e revisões independentes. Cadastro centrado nos usuários internos; não cobre todo o modelo amplo de pessoas sem login, famílias, múltiplos vínculos e importação legada. |
| Contribuições | Regras com vigência, destinatários, geração mensal idempotente, desconto/isenção/cancelamento por ajuste aprovado e saldos. Não há negociação completa de acordos parcelados nem interface de rateio automático de famílias. |
| Recebimentos | Conferência manual identificada, pagamento por terceiro, alocação entre meses/membros autorizada pela tesouraria, excesso como saldo, recibo individual sanitizado. Não há confirmação real sem fonte financeira. |
| Tesouraria | Contas, caixa, transferências, despesas, reembolsos, adiantamentos, aprovações, devoluções manuais registradas e fechamento revisado. Não é uma contabilidade formal, não executa pagamentos bancários, não contém toda a gestão de despesas recorrentes/parcelamento. |
| Conciliação | CSV documentado, totais, importações sobrepostas e associação manual. OFX/PDF de extrato ainda não têm importador próprio. Não existe consulta pública universal a Pix. |
| Pix | Somente Efí Sandbox opcional. Sem credenciais de banco escolhidas, testes de provedor, webhook homologado ou dinheiro real. Polling não é apresentado como webhook. Devoluções identificadas no provedor podem exigir revisão e registro manual autorizado. |
| Comprovantes | Arquivos privados, hash, limite, quarentena, antimalware isolado, análise estrutural, extração textual de PDF e mensagens de revisão. **Sem OCR de prints**, comparação visual avançada nem validação criptográfica de assinatura/cadeia bancária. Metadados/texto não autenticam pagamento. |
| Estoque | Lotes/validade, quantidades exatas em milésimos, reservas, entrega, consumo, retorno, perda, contagem aprovada e histórico. Conversão automática entre múltiplas embalagens e inventário com leitura de código de barras não estão concluídos. |
| Compras | Solicitação, envio para aprovação, aprovação por pessoa distinta, criação de despesa e recebimentos parciais. Comparativo formal de várias cotações, devolução ao fornecedor e alterações complexas de pedido não estão concluídos. |
| Doações/fundos | Promessa e recebimento distintos; dinheiro, materiais, bens, serviços; finalidade e saldo vinculado. Sem cadastro completo de beneficiários e distribuição de cestas em módulo social específico. |
| Patrimônio | Cadastro, origem, empréstimo, retorno, localização e manutenção. Vida contábil, depreciação e descarte aprovado com fluxo próprio não foram implementados. |
| Eventos | Cadastro, participantes, atualizações, vínculo com limpeza, avisos, tarefas e referências financeiras. Não inclui bilheteria/vendas nem todo o planejamento avançado de equipes. |
| Limpeza | Equipe-alvo, mutirão de membros ativos, série semanal, resposta, dispensa administrativa, troca aceita/aprovada, reagendamento, tarefas, participação e encerramento. Reserva não retirada é liberada no encerramento; material entregue permanece pendente de prestação. Rodízio automático, vagas voluntárias com fila e públicos gerais filtrados por grupos não estão concluídos. |
| Secretaria | Documentos, arquivos privados, tarefas e manutenção com responsáveis. Modelos de atas, assinatura eletrônica e calendário fiscal/local não estão implementados. |
| Notificações | Central paginada, leitura/abertura/arquivamento distintos, comunicados e ciência, push móvel, horários/preferências, versões/revisões e deduplicação. Não há garantia de entrega, homologação em celulares físicos nem transferência para outro canal. |
| Relatórios | Caixa por data, mensalidades por competência, despesas, fundos, orçamento e CSV sanitizado; impressão de recibos/relatórios. Painel de prestação pública com anonimização dedicada e projeção completa de 30/60/90 dias ainda não estão concluídos. |
| Operação | Scripts de configuração/publicação, Compose privado, cópia criptografada e restauração em projeto novo. Procedimentos escritos, não executados em Docker nesta entrega. |
| Auditoria e proteção | Registro de operações, idempotência, arquivos privados, permissões e revisão. Não há armazenamento externo imutável já contratado, MFA/passkeys completos, teste de invasão, política automatizada de retenção/expurgo nem comprovação de restauração. |

## Pendências que não são apenas credenciais

A implementação **não encerra os 70 cenários da especificação v0.3**. Além de testes integrados, permanecem funcionalidades avançadas da tabela: OCR/assinaturas, importadores adicionais, rodízio/voluntariado/grupos, parte de ações sociais/patrimônio/contabilidade, projeções e controles operacionais de longo prazo.

Não ocultar essas lacunas sob “falta apenas configurar”. A interface não deve oferecer um selo de autenticidade, banco conectado ou função operacional que o servidor não executa. As áreas implementadas têm ações reais no código, mas precisam passar pelos builds e testes do ambiente adequado.

## Bloqueadores de liberação

1. Resolver dependências, gerar lockfile, auditar e obter builds .NET e Next bem-sucedidos.
2. Executar xUnit, HTTP contra replica set, testes de concorrência/duplicidade, processador com ClamAV real e restauração.
3. Revisar lacunas do escopo e completar ou aprovar formalmente a exclusão antes de afirmar cobertura total.
4. Definir provedor financeiro e homologar sem confundir Sandbox com produção.
5. Implantar em HTTPS autorizado, testar Android/iPhone compatíveis, permissão negada, dados móveis, offline, logout e push.
6. Revisar proteção/retenção de dados, perfis, auditoria externa, contingência e responsabilidades da casa.

Até essas verificações: **somente dados fictícios em homologação.** Nenhum repositório remoto, pipeline remoto, servidor, conta de banco, domínio ou loja foi alterado nesta entrega.
