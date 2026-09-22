# Orientações de desenvolvimento

## Produto e escopo

Leia README, docs/STATUS.md e docs/ADR-001-stack-shopair.md antes de modificar código. Stack: Next/React/Tailwind + .NET10/MongoDB. Não substituir por Expo ou PostgreSQL e não incorporar este módulo aos bancos ou repositórios comerciais da ShopAir sem autorização explícita.

A especificação v0.3 é a referência do produto completo. A versão 0.2 acrescenta serviços e telas de financeiro, estoque, compras, comprovantes, secretaria, limpeza e comunicação. Consulte STATUS.md: há lacunas funcionais e testes integrados não executados. Não confundir existência de código com compilação, homologação ou cobertura integral.

## Invariantes

1. Notificações somente central interna e push móvel. Proibido adicionar WhatsApp, e-mail, SMS ou desktop, inclusive como fallback.
2. Leitura, resposta, presença e tarefa são registros diferentes. Não inferir presença de um clique ou penalizar ausência de resposta.
3. No financeiro: upload não quita; um recebimento não pode ser usado além de seu saldo; correção é rastreável.
4. Toda autorização no servidor, com HouseId e escopo do usuário. Não basta ocultar botões. Não retornar hashes ou tokens.
5. Publicação e notificações/auditoria na mesma transação. MongoDB standalone não é alternativa silenciosa.
6. Nenhum segredo versionado. Nunca subir `.env`, `.secrets`, dumps ou dados pessoais. Nenhuma credencial real em exemplos/testes.
7. Conteúdo autenticado não vai ao cache offline. Não enfileirar gravações offline como se estivessem confirmadas.
8. Não remover bloqueio de Production, testes ou validações apenas para o build passar. Corrigir a causa e documentar limitações.

## Verificação

Executar os testes Node; build Next; dotnet test/build; integração descartável no Compose. O smoke altera a senha do administrador, portanto só executá-lo em banco novo de teste. Não usar `docker compose down -v` em homologação persistente ou produção.

Registrar separadamente: escrito, compilado, testado e publicado. Não declarar sucesso de um teste que não foi executado. Atualizar docs/STATUS.md e relatórios com evidências.
