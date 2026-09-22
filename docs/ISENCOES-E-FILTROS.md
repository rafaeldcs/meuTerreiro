> Documento histórico da versão 0.2.1, preservado para o fluxo **individual e excepcional**. A partir de 0.2.2, a regra principal também existe no cadastro do médium. Leia `ISENCAO-NO-CADASTRO.md`. Os filtros e o detalhe por competência descritos abaixo continuam disponíveis. As contagens de testes abaixo pertencem à revisão anterior.

# Mensalidades: motivo de isenção, detalhes e histórico

**Versão:** 0.2.1-alpha.1 · **Data:** 22/09/2026
**Base:** Terreiro_v0_2_responsivo_codigo.zip (0.2.0-alpha.2).
**Situação:** alterações de código para homologação; não publicadas e sem execução integrada de API/MongoDB.

## Comportamento entregue no código

A tela Mensalidades abre com ano e mês corrente no fuso America/Sao_Paulo. O usuário pode selecionar todos os meses de um ano, um mês específico de todos os anos ou todo o histórico. Os anos do seletor vêm das competências existentes no histórico autorizado, acrescidos do ano corrente. Todos os 12 meses são selecionáveis. Registros só aparecem quando gerados; selecionar um mês vazio não cria mensalidades e não supõe quitação.

Os filtros de ano, mês, situação e busca são enviados ao servidor e aplicados antes da contagem e da paginação. A troca de filtro volta à primeira página. A busca é por nome do membro ou competência e é aplicada ao enviar Buscar. O filtro Pagas inclui isentas regularizadas; Somente isentas permite distingui-las. A API restringe membros aos próprios dados e a tesouraria à casa autorizada.

O comando Isentar mensalidade pede somente **motivo**, obrigatório, entre 5 e 500 caracteres. Nenhum título ou cargo é exigido. Trata-se de uma isenção daquela mensalidade, não de um benefício recorrente nem de uma alteração de cadastro.

O servidor carrega o valor vigente, valida a revisão da mensalidade, o período aberto e a ausência de pagamentos, calcula a redução e cria uma solicitação no fluxo de aprovação existente. O membro não pode conceder isenção. A revisão exige outro responsável; quem recebe o benefício também não pode aprová-lo. Solicitações repetidas com o mesmo identificador não duplicam a operação; uma segunda solicitação de ajuste na mesma versão é recusada enquanto a primeira estiver pendente.

Após aprovação, a mensalidade aparece visualmente como **Pago** com a observação **Isento**. Internamente permanece `State = Exempt`, `PaidCents = 0`, `BalanceCents = 0`, com redução identificada em `AdjustmentCents`. Não é criado Receipt, LedgerEntry, comprovante bancário nem data fictícia de pagamento. A observação não altera relatórios de caixa ou arrecadação efetiva.

Tocar na competência, na situação ou em Ver detalhes abre:

- Membro, competência e vencimento.
- Valor original, ajustes, valor recebido destinado ao mês e saldo.
- Motivo da isenção, data de aprovação e identificação do aprovador para a equipe financeira.
- Pagamentos realmente vinculados: data do crédito, método de verificação e valor destinado a esta mensalidade, com acesso ao recibo autorizado.
- Histórico das solicitações/ajustes, incluindo os ainda pendentes ou rejeitados.

Pagamentos agrupados não expõem ao membro os valores e beneficiários de outras pessoas. Notas internas de revisão não são devolvidas por esse endpoint. Nos registros antigos, o motivo pode ser recuperado da aprovação já existente; quando não disponível, a interface informa a ausência, sem inventar justificativa.

## Exemplo de apresentação (fictício)

| Campo | Exemplo |
|---|---|
| Competência | Setembro de 2026 |
| Situação | Pago · Isento |
| Valor original | R$ 50,00 |
| Ajuste / isenção | -R$ 50,00 |
| Recebido | R$ 0,00 |
| Saldo | R$ 0,00 |
| Motivo | Função exercida no centro |

## Casos tratados e limites

Isenção integral pelo botão simples não pode apagar recebimentos já registrados: mensalidades parcial ou integralmente pagas exigem revisão no fluxo financeiro existente. Esta revisão não implementa isenção automática de saldo remanescente nem devolução automática.

Uma isenção pendente de aprovação não muda o estado para pago. Períodos fechados e revisões concorrentes são validados no backend. Nenhuma alteração retroage a outras competências, dispensa limpeza ou muda permissões. Um registro antigo incoerente (marcado isento, mas com saldo ou valor pago) aparece como Isenção a revisar, não como quitação comprovada.

Os campos novos são opcionais no modelo e não exigem preenchimento manual dos registros antigos. A inicialização cria índices auxiliares de competência e histórico sem remover dados. Faça backup e valide em homologação antes de atualizar uma base existente; esta entrega não executou migração ou publicação em servidor.

As notificações permanecem na central e no push móvel. A aprovação gera Mensalidade atualizada, não confirmação de dinheiro recebido. Não há WhatsApp, e-mail ou SMS.

## Arquivos principais

`app/src/components/Dues.jsx`, `app/src/lib/dues.mjs`, `app/src/lib/modules.mjs`, `app/src/components/Administration.jsx`, `app/src/app/[area]/page.jsx`, `app/src/app/globals.css`.

`api/Domain/DueRules.cs`, `api/Domain/AdministrationModels.cs`, `api/Service/FinanceService.cs`, `api/Service/QueryService.cs`, `api/WebAPI/ModuleEndpoints.cs`, `api/Data/ModuleStore.cs`.

## Validação real desta revisão

162 testes JavaScript aprovados, incluindo regras executadas no JavaScript e verificações estáticas de contratos com o código C#. Os testes estáticos não executam regras C# ou transações do MongoDB.

50 cenários de layout isolado aprovados no Chromium: dez fixtures em 320, 390, 768, 1024 e 1440 pixels de largura. Incluem a listagem de mensalidades, detalhes de isenção e formulário do motivo. O harness usa JSX com hooks simplificados, dados sintéticos e ícones substitutos: não equivale a React/Next em execução nem a testes físicos de dispositivos.

Análise sintática de 44 arquivos JS/JSX sem erros. Acrescentados testes C# em `DueRulesTests.cs` e cenários HTTP em `scripts/smoke.mjs`; não executados. `dotnet` e `docker` indisponíveis; `npm run build` falhou com `next: not found`, sem dependências instaladas.

Os bloqueadores e funcionalidades pendentes dos demais módulos continuam descritos em STATUS.md. Esta revisão não constitui liberação para produção.
