# Validação — 0.2.1-alpha.1 — 22/09/2026

Revisão da tela de mensalidades, isenção por motivo e filtros. Código não publicado, API não homologada.

| Execução nesta revisão | Resultado | Evidência |
|---|---|---|
| `node --test app/tests/*.test.mjs` | 162 aprovados, zero falhas | `reports/isencoes/node-tests.tap` |
| Parser JS/JSX | 44 arquivos, zero erros sintáticos | `reports/syntax.json` |
| JSX isolado + Chromium | 50 cenários, zero falhas | `reports/responsive.json` |
| Shell do script de publicação | `bash -n` sem erro | Checagem sintática, sem deploy |
| Next build | Falhou: `next: not found` (exit 127) | `reports/isencoes/environment.json` |
| SDK .NET / Docker | Executáveis indisponíveis | `reports/isencoes/environment.json` |

Os testes JavaScript incluem lógica de apresentação e verificações estáticas de fonte. Não executam C# ou MongoDB. Os testes C# e HTTP acrescentados não são contados como aprovados.

As 50 verificações de layout usam dez fixtures em 320×740, 390×844, 768×1024, 1024×768 e 1440×1000, incluindo os três cenários novos de mensalidades. O harness transpila JSX e usa hooks simplificados e dados sintéticos; não executa React/Next, autenticação ou requisições de negócio. Ícones substitutos são usados nas prévias. Não há instalação nem push real testado no aparelho.

O processador de documentos, backup/restauração, pagamento bancário, integração do aplicativo e implantação não foram executados nesta revisão. A execução anterior do processador permanece exclusivamente no relatório histórico `VALIDACAO-v0.2.0-historica.md`.

Rode builds, xUnit, testes HTTP contra replica set descartável e homologação física antes de usar registros reais. O smoke acrescenta isenção, revisão independente, filtros, isolamento entre membros, preservação de caixa e pagamento agrupado. Ele cria dados e altera senhas: não executar em produção nem em banco da comunidade.

Regras e escopo: `ISENCOES-E-FILTROS.md`. Demais pendências: `STATUS.md`.
