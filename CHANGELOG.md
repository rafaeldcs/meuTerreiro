# Histórico

## Isenção vinculada ao cadastro do médium — 0.2.2-alpha.1

A regra recorrente agora pertence a um médium cadastrado (`MemberExemption.MemberId`), sem cadastro de títulos. Em **Membros → Isenção de mensalidade**, a equipe autorizada informa o motivo e a primeira competência. O fim é opcional; vazio significa sem término definido. Uma aprovação independente ativa a regra para todas as mensalidades geradas na vigência.

Mensalidades existentes, ainda sem pagamento e em períodos abertos, são regularizadas durante a aprovação. As demais ficam preservadas e identificadas no cadastro para revisão. Nas competências abrangidas, a interface mantém **Pago · Isento** e os detalhes informam a origem no cadastro. Não há entrada de caixa nem criação de recibo de dinheiro.

O encerramento aprovado retoma a cobrança a partir do próximo mês ou posterior e preserva meses anteriores. Mensalidades futuras já isentadas por essa regra têm somente o ajuste dessa origem revertido. Isenções individuais antigas não se tornam benefícios permanentes. A ação por competência permanece como **Isentar somente este mês (exceção)**.

Guia: `docs/ISENCAO-NO-CADASTRO.md`. Resultados desta revisão: **195 testes JavaScript aprovados**, **60 verificações de layout isolado** e **47 arquivos JS/JSX analisados sem erros de sintaxe**. C#, MongoDB, build Next e integração HTTP não foram validados; não é publicação em servidor nem liberação de produção.


## 0.2.1-alpha.1 — 22/09/2026

Mensalidades: motivo único para solicitar isenção; cálculo no servidor; aprovação com histórico e sem caixa fictício; situação visual Pago · Isento; consulta de detalhes financeiros limitada à mensalidade autorizada; filtros por mês/ano/situação e busca antes da paginação; padrão mês corrente e acesso ao histórico. Interface responsiva e testes específicos. Controles financeiros e notificações exclusivamente no aplicativo preservados.

Executados: 162 testes JS, 50 cenários de layout isolado e análise sintática de 44 arquivos JS/JSX. Testes C# e HTTP acrescentados, não executados. Sem publicação.


## 0.2.0-alpha.2 — 21/09/2026

Ampliados os módulos funcionais de administração e a interface responsiva; adicionados serviços de financeiro, conciliação, estoque/compras, doações, patrimônio, eventos, documentos, orçamento, limpeza avançada, permissões, recuperação e notificações. Adaptador Efí Sandbox opcional e processador privado de evidências com ClamAV.

Correções na persistência de alocações de Pix, redação de mensagens internas, versão de acessos, autorização concorrente, transição de reserva ao encerrar limpeza, campos numéricos exatos, data opcional de documentos e navegação dos avisos. Removida criação irrestrita de perfis privilegiados; configuração do segundo administrador é uma exceção inicial explícita.

Menu e formulários adaptados a celular/tablet/computador. Tabelas passam a cartões quando as ações não cabem. Incluídos scripts de backup/ensaio de restauração, pipeline ampliado e novos testes.

Resultados locais: 138 verificações JS, 18 do processador e 35 cenários de layout. Sem build .NET/Next concluído, sem API/MongoDB executados, sem Pix ou push real homologados e sem publicação externa. Persistem lacunas de escopo, documentadas em STATUS.md.

## 0.1.0-alpha.1

Primeiro incremento com autenticação, limpeza básica, central interna, estrutura PWA, Web Push e infraestrutura inicial.
