# Isenção de mensalidade no cadastro do médium

**Versão:** 0.2.2-alpha.1 · **Data:** 22/09/2026  
**Base:** Terreiro_v0_2_1_isencoes_mensalidades.zip.  
**Situação:** código alterado, sem publicação ou homologação da API/banco.

## O que foi corrigido

A versão anterior isentava uma mensalidade específica. Esta atualização acrescenta uma regra por **médium cadastrado**. O motivo é informado uma vez; as próximas mensalidades geradas durante a vigência recebem a mesma regra aprovada. Não existe dependência de título, cargo religioso, função de limpeza ou perfil de acesso.

## Como usar

Em **Membros**, a equipe com acesso financeiro abre **Isenção de mensalidade** no cartão do médium. A tela consulta o cadastro e o histórico de isenções dessa pessoa. Pessoas com acesso apenas à coordenação não recebem esses motivos financeiros.

O formulário **Isentar este médium** contém motivo obrigatório, primeira competência (mês corrente por padrão) e última competência opcional. Deixar a última competência vazia define vigência sem prazo, até encerramento aprovado. Não é necessário repetir o pedido a cada mês.

A concessão fica pendente até outro responsável autorizado aprovar em **Aprovações**. O solicitante e o próprio beneficiário não podem aprovar seu benefício. Alterar ou rejeitar uma solicitação não depende de mensagem externa. O solicitante pode retirar um pedido de concessão ainda pendente e enviar outro corrigido.

Depois de concedida a isenção, o cadastro mostra **Isento de mensalidade**, motivo, vigência e histórico. Não oferece uma segunda concessão sobre um benefício sem término. Benefícios com fim conhecido permitem programar a próxima concessão sem sobreposição. A API revalida tudo, mesmo se o cliente manipular os campos.

## Mensalidades abrangidas

**Geração de novas competências:** `dues.generate` consulta a regra aprovada do médium antes de inserir cada mensalidade. A regra não cria obrigações por conta própria: continua sendo necessário gerar as mensalidades pelo processo existente da casa. Selecionar um mês no filtro não gera registros.

**Mensalidades já geradas:** durante a aprovação, competências na vigência, sem pagamento, não canceladas, com saldo e em período aberto recebem a isenção. Mensalidades pagas ou parcialmente pagas, canceladas, já regularizadas ou em período fechado são preservadas. O resultado informa quantas mensalidades foram ajustadas e lista as que não foram alteradas, com motivo. Nada é escondido nem devolvido automaticamente.

**Meses anteriores ao início:** permanecem intactos. Solicitação recorrente retroativa é recusada; decisões sobre competências antigas usam o ajuste individual com revisão. Pedido que atravessou a virada do mês antes da aprovação precisa ser reapresentado com vigência válida.

**Apresentação:** os meses isentos continuam como **Pago · Isento**. O registro financeiro permanece `Exempt`; `PaidCents` é zero. O valor original e a redução ficam separados. Não há criação de `Receipt`, `LedgerEntry`, comprovante bancário ou data fictícia de crédito.

O detalhe da mensalidade guarda uma cópia do motivo, a referência da regra do cadastro e a aprovação. Mesmo que a isenção seja encerrada depois, os meses anteriores mantêm seus motivos. Filtros de mês corrente, todos os meses, ano, situação e histórico continuam disponíveis.

## Encerramento

**Encerrar isenção** pede motivo e a primeira competência que voltará a ser cobrada. O padrão é o próximo mês; a API não aceita transformar a competência corrente ou anteriores em dívida por esse comando. O encerramento também exige revisão independente.

Depois de aprovado, competências futuras geradas deixam de receber a regra. Para as já geradas a partir da competência de retorno, o servidor desfaz somente a redução proveniente dessa isenção, restaurando outros descontos que existiam antes. Se uma dessas competências estiver fechada, todo o encerramento é recusado até revisão/reabertura, sem conclusão parcial.

Um benefício futuro pode ser encerrado antes de começar, usando a própria competência inicial como limite. O histórico é preservado. Uma isenção com fim definido termina naturalmente após sua última competência, sem exigir desligar o médium.

## Histórico, acesso e limites

O cadastro usa a nova coleção `m_memberexemption`, vinculada ao usuário/médium e à casa, com índice por casa, médium e primeira competência. As mensalidades recebem campos opcionais para a origem e a parcela do ajuste recorrente. Campos antigos e pagamentos não são migrados para isenções recorrentes por inferência.

As gravações usam o controle transacional/idempotente existente e o bloqueio de escrita por casa. Há validação de versão e de aprovação. Sobreposição de duas regras pendentes/aprovadas é recusada; dados antigos inconsistentes são sinalizados, nunca resolvidos escolhendo uma regra silenciosamente.

O próprio médium pode consultar suas regras, sem consultar as de terceiros. A tesouraria consulta o escopo autorizado. Perfil administrativo ou título religioso não concede isenção automática. A regra também não dispensa limpeza e não altera permissões.

A isenção individual anterior fica disponível como exceção, rotulada **Isentar somente este mês (exceção)**. Uma decisão individual posterior sobre uma mensalidade remove a vinculação automática apenas daquele registro; encerramentos futuros não devem sobrescrever essa decisão independente.

Não foram criados WhatsApp, e-mail, SMS ou notificações desktop. Avisos são internos, com push móvel genérico quando habilitado. Falhas de push não alteram a situação financeira.

## Validação

195 testes JavaScript aprovados (33 adicionais nesta revisão), 60 cenários de layout isolado em 320, 390, 768, 1024 e 1440 pixels e 47 arquivos analisados sem erro de sintaxe. Os testes de fonte C# são verificações textuais; não executam C# nem MongoDB.

Foram acrescentados testes de domínio xUnit e cenários HTTP para concessão, reenvio, vigência, preservação de pagamentos, acesso próprio, origem, outra pessoa, encerramento futuro e proibição de autoaprovação. **Não executados neste ambiente.**

.NET e Docker indisponíveis; o build Next falhou porque `next` não está instalado. Nenhum servidor, repositório remoto, banco ou aplicativo publicado foi atualizado. Use dados fictícios e execute o pipeline completo antes de usar a funcionalidade com registros reais. Os demais limites de `STATUS.md` permanecem aplicáveis.
