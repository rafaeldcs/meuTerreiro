# Terreiro — gestão administrativa e interface responsiva

**Versão:** 0.2.2-alpha.1 · **Data:** 22/09/2026  
**Casa de referência:** Centro de Umbanda Caboclo Tenda d’Água  
**Entrega:** código-fonte ampliado para revisão e homologação. **Não é uma versão integralmente compilada, publicada ou aprovada para produção.**

Next.js/React/Tailwind + C#/.NET 10 + MongoDB + Docker, preservando as famílias de tecnologias da ShopAir. Banco, credenciais, arquivos, cookies, subdomínio e volumes são próprios deste projeto. Não é Expo/React Native e não utiliza PostgreSQL.

O aplicativo é uma **PWA responsiva**, instalável pelo navegador compatível depois de publicado em HTTPS. O pacote não é APK/IPA, não está em lojas e não contém um endereço público já implantado.

## Isenção vinculada ao cadastro do médium — 0.2.2-alpha.1

A regra recorrente agora pertence a um médium cadastrado (`MemberExemption.MemberId`), sem cadastro de títulos. Em **Membros → Isenção de mensalidade**, a equipe autorizada informa o motivo e a primeira competência. O fim é opcional; vazio significa sem término definido. Uma aprovação independente ativa a regra para todas as mensalidades geradas na vigência.

Mensalidades existentes, ainda sem pagamento e em períodos abertos, são regularizadas durante a aprovação. As demais ficam preservadas e identificadas no cadastro para revisão. Nas competências abrangidas, a interface mantém **Pago · Isento** e os detalhes informam a origem no cadastro. Não há entrada de caixa nem criação de recibo de dinheiro.

O encerramento aprovado retoma a cobrança a partir do próximo mês ou posterior e preserva meses anteriores. Mensalidades futuras já isentadas por essa regra têm somente o ajuste dessa origem revertido. Isenções individuais antigas não se tornam benefícios permanentes. A ação por competência permanece como **Isentar somente este mês (exceção)**.

Guia: `docs/ISENCAO-NO-CADASTRO.md`. Resultados desta revisão: **195 testes JavaScript aprovados**, **60 verificações de layout isolado** e **47 arquivos JS/JSX analisados sem erros de sintaxe**. C#, MongoDB, build Next e integração HTTP não foram validados; não é publicação em servidor nem liberação de produção.

## O que esta entrega acrescenta no código

| Área | Fluxos escritos |
|---|---|
| Mensalidades | Regras com vigência, geração por competência, exceções aprovadas, saldo parcial e recibos individuais. |
| Financeiro | Contas, recebimentos, destinação entre mensalidades, crédito restante, transferências, despesas, reembolsos, adiantamentos, aprovações, devoluções, caixa e fechamento. |
| Conciliação | Importação CSV, checagem de totais, controle de duplicidade e vinculação manual independente da origem do arquivo. |
| Comprovantes | Upload privado, hash, quarentena, antimalware em serviço isolado, leitura do texto de PDF e casos de revisão com mensagens restritas. |
| Doações e fundos | Promessa separada do recebimento; dinheiro, material, bens e serviços; destinação financeira e saldo reservado. |
| Estoque e compras | Itens/lotes/validade, reserva, retirada, consumo, devolução, perda, contagem aprovada, pedido de compra, recebimento parcial e despesa vinculada. |
| Administração | Fornecedores, eventos, patrimônio, empréstimos, manutenção, tarefas, documentos e orçamento. |
| Limpeza | Equipe-alvo ou mutirão, tarefas, respostas, participação verificada, dispensa, troca aceita/aprovada, reagendamento, série semanal e vínculo com evento. |
| Comunicação | Central individual paginada, abertura/leitura/arquivamento separados, avisos móveis, preferências, horários e comunicados com ciência explícita. |
| Segurança | Permissões explícitas, recuperação por códigos de uso único, reautenticação, revisão independente de acessos, histórico e migração legada autorizada. |
| Pix | Adaptador **opcional Efí Sandbox**, cobrança/consulta autenticada e conciliação de valores simulados. Desligado por padrão, sem dinheiro real. |
| Operação | Compose isolado, processador privado, scripts de publicação, backup criptografado e ensaio de restauração em projeto novo. |

Há 37 áreas administrativas no catálogo de telas, além de login, início, configurações, notificações, limpeza, relatórios e recuperação. **Quantidade de telas não é evidência de cobertura integral da especificação.** Consulte `docs/STATUS.md` e `docs/COBERTURA.md` para as lacunas reais.

## Responsividade

Menu lateral em telas maiores; navegação inferior e menu móvel em telas menores. Formulários reorganizam as colunas, listas financeiras passam a cartões, identificadores longos quebram linha e diálogos respeitam a altura disponível. Ações principais têm alvos de toque com pelo menos 44 px nos cenários verificados.

Foram executados **60 cenários de layout isolado** em 320, 390, 768, 1024 e 1440 pixels: todos passaram. São doze fixtures de componentes reais com dados sintéticos, hooks simplificados e ícones de teste. **Não são testes end-to-end do React/Next e não comprovam execução de API, autenticação ou push.** Capturas e medições: `docs/reports/screenshots/` e `docs/reports/responsive.json`.

## Validação efetivamente executada

| Verificação | Resultado |
|---|---|
| Testes JavaScript de regras auxiliares, contratos de fonte, navegação, service worker e estrutura | 195 aprovados nesta revisão. |
| Processador Python: documentos sintéticos e protocolo de scanner com conexão simulada | 18 aprovados na revisão anterior; não reexecutados nesta. Sem ClamAV real naquele ensaio. |
| Disposição dos componentes no Chromium | 60 cenários aprovados, dentro do escopo acima. |
| Parser JavaScript/JSX e scripts | 47 arquivos sem erro sintático. Não equivale a compilação Next. |
| Configurações (histórico anterior) | XML/JSON/YAML e geração de credenciais verificados na revisão anterior; sem nova execução completa nesta entrega. |
| Build Next | Tentado nesta revisão e não concluído: `next` não está instalado (exit 127). |
| .NET/xUnit, MongoDB/Docker, integração HTTP e restauração | Não executados neste ambiente; ferramentas ausentes. Código e scripts de teste estão incluídos. |
| URL pública, aparelhos Android/iPhone e push real | Não publicados/não testados. |

`docs/VALIDACAO.md` explica as versões efetivamente usadas e os limites. Não somar os testes escritos, mas não executados, aos resultados aprovados.

## Estrutura

```text
app/                    Interface responsiva Next.js, React, Tailwind, PWA e testes JS
api/Domain/             Entidades e regras determinísticas
api/Data/               MongoDB, índices, transações e idempotência
api/Service/            Financeiro, operações, evidências, limpeza, consultas e segurança
api/WebAPI/             Rotas, permissões, workers, Web Push e Efí Sandbox
api/Tests/              Testes xUnit a executar em ambiente .NET
processor/              Análise isolada PDF/imagem e integração ClamAV
infra/                  Inicialização de replica set e gateway
scripts/                Configuração, testes, publicação, backup e restauração isolada
.github/workflows/      Pipeline de builds, testes e integração descartável
```

## Preparar homologação

Requer Docker/Compose, Node 22, rede para dependências/imagens e capacidade de memória revisada. **Não executar indiscriminadamente na VPS compartilhada da ShopAir:** o scanner é um serviço adicional significativo. O script de publicação recusa, por padrão, iniciar quando encontra menos de 6 GiB livres. Isso é uma margem operacional deste projeto, não garantia de dimensionamento.

Em diretório novo:

```bash
node scripts/init.mjs
node --test app/tests/*.test.mjs
```

Primeiro execute os builds/testes descritos em `docs/VALIDACAO.md` ou o pipeline em repositório privado. Somente depois, em homologação descartável:

```bash
docker compose config --quiet
docker compose up -d --build
```

O endereço local padrão é `http://localhost:8080`. Login inicial: `admin`; senha temporária em `BOOTSTRAP_PASSWORD` no **seu** `.env`, gerada aleatoriamente. O primeiro acesso exige trocar a senha. Não compartilhar `.env`, senha, certificados nem chaves.

Para HTTPS, domínio e celular: **`docs/PUBLICACAO.md`**. O servidor não aceita `Production`; isso evita tratar código ainda não homologado como um sistema validado para dinheiro real.

### Primeiros acessos e aprovação independente

O administrador inicial é um bootstrap explícito com permissões completas para configurar a homologação. A tela de membros cria acessos comuns. Uma exceção de configuração inicial, registrada em auditoria e com reautenticação, permite criar **um segundo administrador técnico**, sem permissões financeiras automáticas. As demais atribuições de perfil são solicitadas e revisadas em **Acessos**, por pessoas distintas; o beneficiário não aprova a própria mudança.

Depois, o administrador solicita a atribuição de Tesouraria a um membro e o segundo administrador a revisa. Despesas/reembolsos exigem aprovador diferente do solicitante. A chave de recuperação é exibida apenas ao gerar os códigos, não enviada por mensagem externa.

### Instalação anterior do alpha

Não apague volumes, não gere novas senhas de banco e não recrie `.env`. Faça backup antes de atualizar. `node scripts/upgrade-config.mjs` acrescenta somente a chave faltante do processador. Usuários do alpha anterior sem campo de permissões exigem migração explícita documentada em `docs/PUBLICACAO.md`. Não é uma atualização silenciosa de privilégios.

## Regras que não mudaram

**Arquivo recebido não é dinheiro recebido.** Nenhum upload, hash, leitura de PDF ou aprovação documental dá baixa financeira. Confirmação manual informa a pessoa e a fonte consultada; integração simulada fica identificada como Sandbox. Reserva de estoque não é consumo; limpeza confirmada não é presença; aviso aberto não é aprovação.

Notificações somente na central interna e push móvel permitido. Sem WhatsApp, e-mail, SMS, outros mensageiros ou desktop, inclusive em falhas.

## Limite desta entrega

Não afirme que está “100% implementado, testado e publicado”. Existem funcionalidades avançadas ainda não fechadas e validações centrais por executar; a relação está em `docs/STATUS.md`. O pacote amplia substancialmente o sistema, mas não elimina a necessidade de compilar, corrigir eventuais falhas encontradas e homologar os fluxos reais antes do uso da comunidade.
